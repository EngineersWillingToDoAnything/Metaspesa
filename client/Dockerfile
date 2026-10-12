FROM node:26.11-alpine AS builder

ARG NEXT_PUBLIC_REST_API_URL=http://localhost:4001/api/v1
ENV NEXT_PUBLIC_REST_API_URL=$NEXT_PUBLIC_REST_API_URL

WORKDIR /client

RUN npm install -g pnpm@10.34.5

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./

RUN pnpm install --frozen-lockfile

COPY . .

RUN pnpm run build

FROM node:26.11-alpine AS runner

WORKDIR /client

ENV NODE_ENV=production

COPY --from=builder /client/package.json ./
COPY --from=builder /client/pnpm-lock.yaml ./
COPY --from=builder /client/pnpm-workspace.yaml ./
COPY --from=builder /client/node_modules ./node_modules
COPY --from=builder /client/.next ./.next
COPY --from=builder /client/next.config.* ./

ENTRYPOINT ["npm", "run", "start"]