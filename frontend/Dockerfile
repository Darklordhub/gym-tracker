FROM node:20-alpine AS build
WORKDIR /app

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY frontend/ .
RUN npm run build

FROM node:20-alpine AS final
WORKDIR /app

COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci

COPY --from=build /app/dist ./dist
COPY --from=build /app/vite.config.ts ./vite.config.ts

EXPOSE 4173

CMD ["npm", "run", "preview", "--", "--host", "0.0.0.0", "--port", "4173"]
