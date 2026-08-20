# OpenMock

Простой сервер HTTP-моков с веб-интерфейсом. Позволяет задавать эндпоинты
(HTTP-метод, путь, код ответа, Content-Type, тело ответа) и в реальном времени
наблюдать входящие запросы по каждому моку.

## Возможности

- Создание, редактирование и удаление моков через веб-интерфейс
- Ответ мока: произвольный статус-код, Content-Type и тело
- Live-лента входящих запросов под каждым моком (WebSocket, без хранения на сервере):
  время, метод, путь с query string, заголовки и тело запроса
- Отдельная лента запросов, не попавших ни под один мок (ответ 404)
- Моки сохраняются в `mocks.json` рядом с приложением и переживают перезапуск
- Ноль внешних зависимостей: только ASP.NET Core, фронтенд — ванильный JS

## Структура

```
OpenMock/
├── Program.cs      — точка входа: админ-API /api/mocks, WebSocket /ws, матчер моков
├── Models.cs       — MockDefinition и RequestHit
├── MockStore.cs    — хранилище моков (память + mocks.json)
├── LiveFeed.cs     — рассылка запросов в браузеры по WebSocket
└── wwwroot/        — веб-интерфейс (index.html, app.js, style.css)
```

## Локальный запуск (для разработки)

Требуется [.NET SDK 10](https://dotnet.microsoft.com/download) (или новее).

```bash
cd OpenMock
dotnet run --urls http://localhost:5080
```

Открыть в браузере: http://localhost:5080

## Запуск на Linux-сервере

### 1. Публикация

На машине разработки (или прямо на сервере, если там стоит SDK):

```bash
cd OpenMock
dotnet publish -c Release -o publish
```

Это framework-dependent сборка — на сервере понадобится ASP.NET Core Runtime.
Альтернатива — self-contained сборка, тогда на сервере не нужно ничего, кроме
самих файлов (бинарник получится заметно больше):

```bash
dotnet publish -c Release -r linux-x64 --self-contained true -o publish
```

### 2. ASP.NET Core Runtime на сервере

Нужен только для framework-dependent сборки. Пример для Ubuntu/Debian:

```bash
sudo apt-get update
sudo apt-get install -y aspnetcore-runtime-10.0
```

Для других дистрибутивов: https://learn.microsoft.com/dotnet/core/install/linux

### 3. Копирование и запуск

```bash
scp -r publish/ user@server:/opt/openmock/
ssh user@server
cd /opt/openmock
ASPNETCORE_URLS=http://0.0.0.0:5080 dotnet OpenMock.dll
# или для self-contained сборки:
# ASPNETCORE_URLS=http://0.0.0.0:5080 ./OpenMock
```

Открыть порт в firewall, если нужно (пример для ufw):

```bash
sudo ufw allow 5080/tcp
```

### 4. systemd-сервис (автозапуск и перезапуск при падении)

Создать `/etc/systemd/system/openmock.service`:

```ini
[Unit]
Description=OpenMock
After=network.target

[Service]
WorkingDirectory=/opt/openmock
ExecStart=/usr/bin/dotnet /opt/openmock/OpenMock.dll
Environment=ASPNETCORE_URLS=http://0.0.0.0:5080
Environment=ASPNETCORE_ENVIRONMENT=Production
Restart=always
RestartSec=5
User=www-data

[Install]
WantedBy=multi-user.target
```

Активировать:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now openmock
sudo systemctl status openmock
journalctl -u openmock -f   # логи
```

### 5. За reverse proxy (опционально)

Если сервис должен сидеть за nginx на 80/443 порту, пример location-блока
(важно проксировать WebSocket-заголовки, иначе live-лента не заработает):

```nginx
location / {
    proxy_pass         http://127.0.0.1:5080;
    proxy_http_version 1.1;
    proxy_set_header   Upgrade $http_upgrade;
    proxy_set_header   Connection "upgrade";
    proxy_set_header   Host $host;
    proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
}
```

## Docker

В репозитории есть готовый многостадийный `Dockerfile` (сборка в SDK-образе,
запуск на `aspnet:10.0`). Внутри контейнера приложение слушает порт 8080.

```bash
cd OpenMock
docker build -t openmock .
docker run -d --name openmock -p 5080:8080 openmock
```

Открыть в браузере: http://localhost:5080

Сохранение моков между пересозданиями контейнера — монтирование файла
`mocks.json` (файл должен существовать на хосте, хотя бы пустой `[]`):

```bash
echo '[]' > mocks.json
docker run -d --name openmock -p 5080:8080 \
  -v "$PWD/mocks.json:/app/mocks.json" openmock
```

## Админ-API

Интерфейс использует его, но можно дергать и напрямую:

| Метод  | Путь              | Описание              |
|--------|-------------------|-----------------------|
| GET    | `/api/mocks`      | список моков          |
| POST   | `/api/mocks`      | создать мок           |
| PUT    | `/api/mocks/{id}` | обновить мок          |
| DELETE | `/api/mocks/{id}` | удалить мок           |

Тело мока (JSON):

```json
{
  "method": "GET",
  "path": "/api/users",
  "statusCode": 200,
  "contentType": "application/json",
  "body": "[{\"id\": 1, \"name\": \"Ivan\"}]"
}
```

Путь сопоставляется точно, без учёта регистра; query string в сопоставлении
не участвует (`/api/users?x=1` попадёт в мок `/api/users`).

## Ограничения

- Входящие запросы не сохраняются — лента живёт только в открытом браузере
  (до 100 последних записей на мок в DOM)
- Тело запроса в ленте обрезается до 64 КБ
- Аутентификации нет — не выставляйте в открытый интернет без proxy
  с авторизацией
