# Bachata Feedback / Telegram Bot (pet‑project)

Telegram-ориентированная платформа для танцевальной школы: управление расписанием, запись на занятия через опросы, аналитика чата с LLM.

Проект собран ради практики архитектуры: Telegram Bot, RabbitMQ очереди, LLM-анализ, EF Core + MySQL, Docker Compose.

---

## Основные возможности

### 🤖 Telegram Bot (`BachataFeedback.TelegramBot`)
Бот — основной интерфейс для учеников, тренеров и администрации.

**Публичные команды:**

| Команда | Описание | Пример |
|---|---|---|
| `/help` | Список всех команд с примерами | `/help` |
| `/today` | Занятия и ивенты сегодня | `/today` |
| `/tomorrow` | Занятия и ивенты на завтра | `/tomorrow` |
| `/next_event` | Ближайший ивент | `/next_event` |
| `/support` | Где не хватает парней или девушек | `/support` |
| `/summary [N]` | Краткое содержание чата (LLM) | `/summary 200` |

**Команды администратора:**

| Команда | Описание | Пример |
|---|---|---|
| `/add_lesson` | Создать занятие | `/add_lesson 2026-08-01 19:00 16 5 месяцев` |
| `/add_party` | Создать вечеринку | `/add_party 2026-08-01 21:00 Летняя фиеста 50` |
| `/publish {id}` | Опубликовать опрос | `/publish 42` |
| `/close_poll {id}` | Закрыть опрос | `/close_poll 42` |
| `/occurrences` | Список предстоящих | `/occurrences` |
| `/cancel {id}` | Отменить занятие | `/cancel 42` |
| `/profile @user [N]` | Анализ пользователя | `/profile @john_doe 200` |
| `/analytics [N]` | Аналитика чата | `/analytics 200` |

**Уровень занятий** указывается в свободном формате: `5 месяцев`, `полтора года`, `2 года`, `0 месяцев` и т.д.

### 📊 Опросы (Telegram Polls)
На каждый ивент или занятие бот публикует **неанонимный опрос** с вариантами:
- 👦 **Парни** — запись с ролью "male"
- 👧 **Девушки** — запись с ролью "female"
- 🎓 **Тренеры/Организаторы** — не учитываются в балансе на занятиях
- ❌ **Не иду**

Дополнительно в зеркальные чаты отправляется текстовое сообщение с inline-кнопками "Хочу прийти" и "Подробнее".

**Важно:** 
- Если человек голосует, а его профиля нет в системе — профиль создаётся автоматически (email = `tg_{id}@telegram.local`).
- Каждое голосование, изменение голоса или отзыв фиксируется в `PollVoteLog` для будущей аналитики.
- При отзыве голоса запись Attendance помечается `retracted`, а не удаляется.

### 📈 Аналитика чата (RabbitMQ + LLM)
Реализована через:
- `AnalyticsCommandHandler` → публикует задачу в RabbitMQ (`chat_analysis` exchange)
- `ChatAnalysisConsumer` (Worker) → забирает задачу, выбирает сообщения из БД, отправляет в LM Studio
- `AnalysisResultDeliveryService` → доставляет результат обратно в Telegram

Доступные аналитические команды:
- `/summary [100|200|300]` — краткое содержание последних N сообщений (доступно всем)
- `/profile @username [N]` — профиль пользователя по его сообщениям (admin)
- `/analytics [N]` — общая аналитика: активность, темы, тональность (admin)

### 📅 Система Occurrence (занятия/ивенты)
- Каждое занятие или ивент — сущность `Occurrence` с типом (`lesson`, `party`, `trip`, `practice`)
- Occurrence публикуется в один или несколько Telegram-чатов
- В canonical-чат отправляется poll (источник голосования), в остальные — зеркала с inline-кнопками
- Поддерживается баланс парней/девушек (тренеры не учитываются)

### 👤 Профили и роли
- Аутентификация: JWT (ASP.NET Core Identity) + Telegram OAuth
- Роли: Admin, Moderator, Organizer, User
- Авто-создание профиля при голосовании в Telegram

---

## Архитектура (вкратце)

```
┌─────────────────┐     ┌──────────────┐     ┌─────────────────┐
│  Telegram Bot    │────▶│  RabbitMQ    │────▶│  Worker (LLM)   │
│  (ASP.NET Core)  │     │  (очереди)   │     │  ChatAnalysis   │
└────────┬────────┘     └──────────────┘     └─────────────────┘
         │                                              │
         ▼                                              ▼
┌──────────────────────────────────────────────────────────┐
│                    API (ASP.NET Core)                     │
│              + EF Core + MySQL + MinIO                   │
└──────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────┐
│  Frontend        │
│  (React/TS)      │
└──────────────────┘
```

- **API** (ASP.NET Core 8, EF Core + MySQL, Identity/JWT, policy-based auth)
- **TelegramBot** (BackgroundService, Telegram.Bot library)
- **Worker** (BackgroundService): RabbitMQ consumer, LM Studio (OpenAI-compatible)
- **Frontend** (React + TypeScript + Tailwind + i18next) — дополнительный интерфейс
- **Хранилища**: MySQL 8, MinIO (S3), RabbitMQ, ClamAV

---

## Технологии
.NET 8, ASP.NET Core, EF Core (Pomelo MySQL), Identity, JWT, Telegram.Bot, RabbitMQ.Client, MinIO SDK, nClam, ImageSharp, React/TS, Tailwind, axios, i18next, Docker/Compose.

---

## Быстрый старт (локально, через Docker Compose)

Требования: Docker, Docker Compose.

Порты по умолчанию:
- API: 5000
- MySQL: 3306
- MinIO: 9000 (API), 9001 (консоль)
- RabbitMQ: 5672 (AMQP), 15672 (web UI)
- ClamAV: 3310
- Adminer: 8081

```bash
# Поднять инфраструктуру
docker compose up -d mysql minio minio-setup clamav rabbitmq adminer

# Поднять API и Worker
docker compose up -d bachatafeedback.api worker bachatafeedback.telegrambot

# Миграции БД
docker compose exec bachatafeedback.api dotnet ef database update
```

Swagger: http://localhost:5000/swagger

### Настройка Telegram Bot
1. Создать бота через [@BotFather](https://t.me/botfather), получить токен
2. Указать токен в `BachataFeedback.TelegramBot/appsettings.json` или через переменную окружения `Telegram:BotToken`
3. Добавить бота в чаты и назначить администратором
4. Через API `/api/telegram-chats` настроить чаты с указанием purpose (`group_primary`, `events_chat`, `flood_chat`, `all_lessons_feed`)

### Frontend (отдельно)
```bash
cd frontend
npm install
npm start
```

---

## Модели данных (ключевые)

- **Occurrence** — занятие/ивент (тип, дата, уровень, вместимость, баланс парней/девушек)
- **Attendance** — запись человека на занятие (статус: going/not_going/retracted, роль: male/female/trainer)
- **PollVoteLog** — лог каждого действия с опросом (для аналитики)
- **ChatMessage** — сообщения чата (для аналитики и LLM-summary)
- **TelegramChat** — настроенные чаты с указанием purpose
- **User** — пользователь платформы (связь с Telegram через TelegramId)

---

## Лицензия / назначение
Учебное назначение, личное портфолио. Использовать с осторожностью, не для production.