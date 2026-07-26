# Vite 8 requires Node >=20.19 (node:18 fails at startup: rolldown imports
# node:util's styleText, added in Node 20).
FROM node:22-alpine

WORKDIR /app

COPY package*.json ./
RUN npm install

COPY . .

EXPOSE 3000

# --host is required: Vite's dev server binds to localhost only by default,
# which is unreachable from outside the container.
CMD ["npm", "run", "dev", "--", "--host"]
