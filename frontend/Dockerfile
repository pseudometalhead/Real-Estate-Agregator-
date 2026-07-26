FROM node:18-alpine

WORKDIR /app

COPY package*.json ./
RUN npm install

COPY . .

EXPOSE 3000

# --host is required: Vite's dev server binds to localhost only by default,
# which is unreachable from outside the container.
CMD ["npm", "run", "dev", "--", "--host"]
