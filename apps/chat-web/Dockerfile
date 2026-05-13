FROM node:25-alpine AS build
WORKDIR /repo

RUN npm install -g pnpm@10.33.4

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY apps/chat-web/package.json apps/chat-web/
COPY apps/chat-web apps/chat-web

RUN pnpm --dir apps/chat-web install --frozen-lockfile && pnpm --dir apps/chat-web build

FROM nginx:1.27-alpine
COPY --from=build /repo/apps/chat-web/dist /usr/share/nginx/html
