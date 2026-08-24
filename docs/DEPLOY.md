# 🚀 Инструкция по деплою и управлению ботом «Выпрями спину, гора!»

Руководство по развёртыванию, обновлению и обслуживанию Telegram-бота на Linux-сервере (Ubuntu / Debian).

---

## 1. 🔄 Как накатывать обновления (когда изменился код)

### Способ 1: В одну команду с рабочего компьютера (Mac / Linux)

Выполните в терминале в папке проекта на своём компьютере:

```bash
rsync -avz --exclude 'bin' --exclude 'obj' --exclude '.git' --exclude '.gemini' \
  ./ root@<IP_СЕРВЕРА>:/opt/bot/ && \
ssh root@<IP_СЕРВЕРА> "cd /opt/bot && docker compose up -d --build bot"
```

> **Что происходит:** Файлы проекта синхронизируются на сервер, пересобирается контейнер бота и перезапускается на лету. База данных PostgreSQL остаётся нетронутой со всеми сохранёнными данными.

---

### Способ 2: Через Git напрямую на сервере

1. Подключитесь к серверу по SSH:
   ```bash
   ssh root@<IP_СЕРВЕРА>
   ```

2. Перейдите в папку бота и обновите код:
   ```bash
   cd /opt/bot
   git pull
   docker compose up -d --build bot
   ```

---

## 2. 🛠 Управление ботом и полезные команды

Все команды выполняются на сервере в каталоге `/opt/bot`:

### Просмотр логов в реальном времени
```bash
docker logs -f posture-bot
```
*(Для выхода из режима просмотра логов нажмите `Ctrl + C`)*

### Просмотр последних 100 строк логов
```bash
docker logs --tail 100 posture-bot
```

### Проверка статуса контейнеров
```bash
docker ps
```

### Перезапуск бота
```bash
docker compose restart bot
```

### Остановка всех сервисов
```bash
docker compose down
```

### Запуск сервисов в фоновом режиме
```bash
docker compose up -d
```

---

## 3. 🆕 Первоначальное развёртывание с нуля на новом сервере

Если вы запускаете бота на абсолютно новом, чистом сервере:

### Шаг 1: Подключение к серверу
```bash
ssh root@<IP_НОВОГО_СЕРВЕРА>
```

### Шаг 2: Установка Docker и Docker Compose
```bash
apt-get update -y
apt-get install -y docker.io docker-compose-v2
systemctl enable --now docker
```

### Шаг 3: Создание рабочей директории и файла переменных окружения
```bash
mkdir -p /opt/bot
cd /opt/bot
nano .env
```

Вставьте в `.env` ваши конфигурационные данные:
```ini
BOT_TOKEN=242464316:AAFxxhWAurba-hw526Uo6TxjO7WS8B7PIio
POSTGRES_DB=posturedb
POSTGRES_USER=postgres
POSTGRES_PASSWORD=generate_your_secure_password_here
ADMIN_CHAT_ID=
```
*(Сохраните файл: `Ctrl + O`, затем `Enter`, для выхода: `Ctrl + X`)*

### Шаг 4: Копирование файлов проекта на сервер
С локального компьютера:
```bash
rsync -avz --exclude 'bin' --exclude 'obj' --exclude '.git' --exclude '.gemini' \
  ./ root@<IP_НОВОГО_СЕРВЕРА>:/opt/bot/
```

### Шаг 5: Запуск проекта
На сервере:
```bash
cd /opt/bot
docker compose up -d --build
```
