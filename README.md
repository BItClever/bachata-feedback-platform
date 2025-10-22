# Bachata Feedback (pet‑project)
Демо‑платформа для танцевального комьюнити: пользователи оставляют обратную связь друг другу и событиям. Проект собран ради практики архитектуры и инфраструктуры: безопасная медиапайплайн‑обработка, очереди, модерация текста (LLM), роли/права, i18n.

Статус: pet‑проект (без реальных пользователей).

## Основные возможности
Аутентификация: JWT (ASP.NET Core Identity). Роли: Admin, Moderator, Organizer, User.
Авторизация: policy‑based по permission‑клеймам (например, events.create). Синхронизация ролей/прав через /api/admin/roles/sync.
Профили и приватность: настройки видимости рейтингов/текста/фото (UserSettings).
Отзывы:
Пользователь ↔ пользователь: звездочки и текст.
Отзывы о событии: оценки по аспектам и текст.
Pending/Red скрываются от посторонних; модератор/автор видят.
Модерация текста (LLM):
Асинхронно через RabbitMQ: API ставит задачу → Worker вызывает LM Studio → присваивает уровень (Green/Yellow/Red) + краткую причину (RU/EN).
События: создание (permission), join/leave, обложка события, альбом фотографий.
Медиа‑пайплайн:
Проверка сигнатуры (JPEG/PNG/WEBP) → антивирус ClamAV → EXIF wipe → ресайз/перекод в JPEG (ImageSharp) → хранение в MinIO (S3‑совместимо).
Отдача через FilesController с Cache‑Control.
Админ:
Роли/пермишены.
Панель модерации (ручная установка уровня/причины, повторная постановка в очередь).
Жалобы: на отзывы и фото (с удалением всех вариантов из хранилища).
i18n фронта: RU/EN.
## Архитектура (вкратце)
API (ASP.NET Core 8, EF Core + MySQL, Identity/JWT, policy‑based auth).
Worker (BackgroundService): чтение из RabbitMQ, вызов LM Studio (OpenAI‑совместимое API), обновление статуса модерации.
Хранилища/сервисы: MySQL 8, MinIO (S3), ClamAV, RabbitMQ.
Frontend: React + TypeScript + Tailwind + i18next.
Docker Compose‑стек для локального запуска.
## Технологии
.NET 8, ASP.NET Core, EF Core (Pomelo MySQL), Identity, JWT, Role/Claim Policies, RabbitMQ.Client 7.x, MinIO SDK, nClam, ImageSharp, React/TS, Tailwind, axios, i18next, Docker/Compose.

## Быстрый старт (локально, через Docker Compose)
Требования: Docker, Docker Compose.

Порты по умолчанию:

API: 5000 (прокси на 8080 внутри контейнера)
MySQL: 3306
MinIO: 9000 (API), 9001 (консоль)
RabbitMQ: 5672 (AMQP), 15672 (web UI)
ClamAV: 3310
Adminer: 8081
Шаги:

Поднять инфраструктуру и API/Worker:


docker compose up -d mysql minio minio-setup clamav rabbitmq adminer
docker compose up -d bachatafeedback.api worker
Swagger API: http://localhost:5000/swagger
Health: http://localhost:5000/health
MinIO console: http://localhost:9001 (логин/пароль — см. docker-compose.yml)
RabbitMQ UI: http://localhost:15672 (guest/guest)

Создать первого администратора:

POST http://localhost:5000/api/admin/create-admin
json

{
  "email": "admin@example.com",
  "password": "admin123",
  "firstName": "Admin",
  "lastName": "User"
}
Логин: POST /api/auth/login.
Синхронизировать роли/права (опционально):
POST /api/admin/roles/sync (требует роль Admin).

(Опционально) LM Studio для модерации:

Запустить LM Studio локально на http://localhost:1234 и скачать модель (по умолчанию qwen/qwen3-14b/8b — см. Worker/Docker env).
В docker‑compose worker уже настроен на host.docker.internal:1234 (для Linux добавлен extra_hosts).
Фронтенд (отдельно):

В .env(.local) укажи REACT_APP_API_URL=http://localhost:5000/api
Запусти фронт (npm/yarn/pnpm) и открой в браузере.
Для внешнего доступа можно пробросить фронт через Cloudflare Tunnel (про API — отдельно).
Основные эндпоинты (срез)
Auth: POST /api/auth/register, /api/auth/login, GET /api/auth/me
Users: GET /api/users, /api/users/paged, GET /api/users/{id}, PUT /api/users/{id}
Reviews: CRUD для отзывов, GET /api/reviews/user/{userId}, POST /api/reviews
Events: GET /api/events, /api/events/paged, GET /api/events/{id}, POST /api/events (policy: events.create), POST /{id}/join|leave, POST /{id}/cover
Photos:
User: POST /api/userphotos/me/upload, POST /api/userphotos/me/set-main, PATCH /api/userphotos/{photoId}/focus, GET /api/userphotos/me, GET /api/userphotos/user/{userId}
Event: GET /api/events/{eventId}/photos, POST /api/events/{eventId}/photos, DELETE /api/events/{eventId}/photos/{photoId}
Files (отдача): GET /api/files/users/{userId}/photos/{photoId}/{size}, /api/files/events/{eventId}/photos/{photoId}/{size}, /api/files/events/{eventId}/cover/{size}
Moderation (admin/moderator): /api/admin/moderation/*
Roles (admin): /api/admin/roles/*, /api/admin/roles/permissions
Reports: POST /api/reports (жалобы), /api/admin/reports (просмотр/resolve)
Роли/права (срез)
Roles: Admin, Moderator, Organizer, User.
Permissions (пример): events.create, events.update, events.delete, moderation.*
Политики настраиваются в Program.cs из Permissions.All. RolePermissionMap задаёт соответствие ролей/прав.
## Заметные ограничения (как есть)
Это pet‑проект: нет email‑подтверждения и refresh‑токенов; пермишены продублированы в JWT и добавляются трансформером (MVP‑компромисс).
FilesController [AllowAnonymous] (кэшируемая выдача картинок): листинги защищены настройками приватности, но прямые URL не проверяют приватность — сознательно в рамках демо.
Воркер использует polling (BasicGet); прод‑вариант — consumer + QoS/DLQ.
Автотестов нет.
## Лицензия / назначение
Учебное назначение, личное портфолио. Использовать с осторожностью, не для production.
