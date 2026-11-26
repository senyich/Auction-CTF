# Trip Auction
<span>This auction not only provides a platform for exchanging items, but also an opportunity to freely manipulate your assets </span>
<div>
  <img width="1278" height="944" alt="Снимок экрана (211)" src="https://github.com/user-attachments/assets/00f09438-1238-4599-8781-2e20f06d6fcf" />
  <img width="1278" height="946" alt="Снимок экрана (212)" src="https://github.com/user-attachments/assets/16df8444-9034-4420-8b58-bf99e4ebe8e0" />
</div>

## Run with Docker Compose

From the repository root, run:

```sh
docker compose up --build
```

Open <http://localhost:5057>. Set `AUCTION_PORT` to choose a different host port.
DataServer is accessible to Auction over the Compose network. SQLite databases
are initialized from the repository copies on first start and persist in named
volumes.
