# jellyfin-plugin-kinopoisk

Fetches metadata from https://www.kinopoisk.ru/. This site is popular in the Russian-speaking community and contains almost no English-language information, so further description will be in Russian.

## Local build

```bash
VERSION=10.10.4.0
docker run --rm -v "$PWD":/src -w /src -u "$(id -u):$(id -g)" -e HOME=/tmp \
  mcr.microsoft.com/dotnet/sdk:9.0 \
  dotnet build --configuration Release --property:Version=$VERSION ./src

zip --junk-paths dist/kinopoisk/jellyfin-plugin-kinopoisk_$VERSION.zip \
  src/Jellyfin.Plugin.Kinopoisk/bin/Release/net9.0/Jellyfin.Plugin.Kinopoisk.dll \
  src/KinopoiskUnofficialInfo.ApiClient/bin/Release/net9.0/KinopoiskUnofficialInfo.ApiClient.dll
```

## Установка

Администрирование - Панель - Расширенное - Плагины - вкладка Репозитории - добавить адрес https://raw.githubusercontent.com/petrows/jellyfin-plugin-kinopoisk/master/dist/manifest.json.

После этого на вкладке Каталог найти "КиноПоиск" (раздел Метаданные) и установить.

Альтернатива для Emby - [luzmane/emby.kinopoisk.ru](https://github.com/luzmane/emby.kinopoisk.ru)

## Настройка

Параметры плагина искать в: Администрирование - Панель - Расширенное - Плагины - вкладка "Мои плагины" - КиноПоиск - "три точки" - Параметры

Если плагин не работает или работает плохо - попробуйте зарегистрировать (и указать в параметрах) свой собственный ApiToken (на сайте https://kinopoiskapiunofficial.tech). По-умолчанию прописан общий, ограничение порядка 10 запросов/сек - для общего ApiToken быстро заканчивается.

## Использование

Поддерживаются:
- Фильмы
- Сериалы

На данный момент грузятся:
- Рейтинг
- Описание
- Постеры и задники
- Актёры
- Трейлеры (только те, что лежат на ютубе - Jellyfin-Web не умеет играть трейлеры, лежащие на самом КиноПоиске)

Плагин будет пытаться найти в имени файла (для фильмов) или имени корневой папки (для сериалов) паттерн вида "kp-12345" или "kp12345", где число - id фильма на сайте КиноПоиск.
