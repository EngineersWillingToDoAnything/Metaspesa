import logging

import httpx

from application.abstractions import (
    FallbackProductRepository,
    MarketWebScraper,
    ProductRepository,
)
from application.product_processors import (
    BrandExtractor,
    BrandSimplifier,
    ProductProcessor,
    QuantityRedundancyRemover,
    QuantityUnitOfMeasureExtractor,
    StringSanitizer,
)
from application.use_cases import (
    RetryFailedSavesCommandHandler,
    ScrapeMarketsCommandHandler,
)
from config import AppConfig
from infrastructure.local_storage import CsvProductRepository
from infrastructure.market_scrapers.market_web_scraper_factory import (
    MarketWebScraperFactory,
)
from infrastructure.playwright_driver import PlaywrightDriver
from infrastructure.rest.rest_product_repository import RestProductRepository
from infrastructure.secrets import LocalSecretVault, SecretNotFoundError
from infrastructure.system_clock import SystemClock
from infrastructure.telemetry.instrumented_playwright_driver import (
    InstrumentedPlaywrightDriver,
)
from infrastructure.web_driver import WebDriver

__logger = logging.getLogger(__name__)


def __create_market_web_scrapers(
    settings: AppConfig, web_driver: WebDriver
) -> dict[str, MarketWebScraper]:
    factory = MarketWebScraperFactory(settings, web_driver)
    return {name: factory.create(name) for name in settings.markets}


def __create_product_processor(settings: AppConfig) -> ProductProcessor:
    first_processor = StringSanitizer()
    first_processor.next(QuantityUnitOfMeasureExtractor()).next(
        QuantityRedundancyRemover()
    ).next(BrandSimplifier(settings.processor.replacements)).next(
        BrandExtractor(settings.processor.known_brands)
    )
    return first_processor


def __create_main_repository(
    settings: AppConfig,
    http_client: httpx.AsyncClient,
) -> ProductRepository:
    vault = LocalSecretVault()
    try:
        username = vault.read_secret(settings.credentials.username_secret)
        password = vault.read_secret(settings.credentials.password_secret)
    except SecretNotFoundError:
        __logger.warning(
            "Scraper credentials were not found; continuing with empty credentials. "
            "Repository authentication will fail unless the server accepts them.",
            extra={
                "username_secret": settings.credentials.username_secret,
                "password_secret": settings.credentials.password_secret,
            },
        )
        username = ""
        password = ""
    return RestProductRepository(
        http_client,
        username,
        password,
    )


def __create_fallback_repository(settings: AppConfig) -> FallbackProductRepository:
    return CsvProductRepository(settings.fallback_persistence.folder_path)


def create_scrape_handler(
    settings: AppConfig,
    web_driver: WebDriver,
    http_client: httpx.AsyncClient,
) -> ScrapeMarketsCommandHandler:
    return ScrapeMarketsCommandHandler(
        main_repository=__create_main_repository(settings, http_client),
        fallback_repository=__create_fallback_repository(settings),
        market_web_scrapers=__create_market_web_scrapers(settings, web_driver),
        product_processor=__create_product_processor(settings),
        clock=SystemClock(),
    )


def create_retry_handler(
    settings: AppConfig,
    http_client: httpx.AsyncClient,
) -> RetryFailedSavesCommandHandler:
    return RetryFailedSavesCommandHandler(
        fallback_repository=__create_fallback_repository(settings),
        main_repository=__create_main_repository(settings, http_client),
    )


async def create_web_driver() -> WebDriver:
    raw_driver = await PlaywrightDriver.create(headless=True)
    web_driver = InstrumentedPlaywrightDriver(raw_driver)
    return web_driver
