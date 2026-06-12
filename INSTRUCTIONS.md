Steam UE Deploy Tool (SDT)
===========================

Инструмент для сборки Unreal Engine 4/5 проектов и деплоя в Steam.
Работает на Windows, macOS и Linux.


Требования
----------

1. .NET 9.0 Runtime
   Скачать: https://dotnet.microsoft.com/download/dotnet/9.0

2. SteamCMD
   Скачать: https://developer.valvesoftware.com/wiki/SteamCMD
   Распаковать в любую папку и добавить в PATH.

3. Unreal Engine (4.27+ или 5.x)
   Должен быть установлен. Инструмент сам найдёт движок через .uproject.
   Если автоопределение не срабатывает — укажи папку движка вручную
   в поле "Custom Engine Path" с кнопкой "..." в Build Config.

4. Steamworks аккаунт
   Нужен для загрузки билдов. Приложение должно быть создано
   в Steamworks Partner Portal.


Быстрый старт (CLI)
-------------------

### 1. Интерактивное создание профилей

    sdt init

Утилита спросит:
- Имя проекта и путь к .uproject
- Платформу и конфигурацию сборки
- Steam App ID, Depot ID, путь к билду
- Имя ветки (branch)

### 2. Управление Steam аккаунтами

    sdt account add                # Добавить аккаунт
    sdt account list               # Показать все
    sdt account login --u mylogin  # Залогиниться (запросит Steam Guard)
    sdt account logout -u mylogin  # Выйти
    sdt account delete -u mylogin  # Удалить

### 3. Сборка

    sdt build -p "MyProject Win64 Shipping"

### 4. Деплой

    sdt deploy -p "MyProject Staging" -d "D:\Output\Windows"

### 5. Сквозной Push

    sdt push -p "MyProject Push"

### 6. Профили

    sdt profile list
    sdt profile delete -t build -n "MyProject Win64 Shipping"


GUI (Desktop)
-------------

Запустить:
    SteamUEDeployTool.Desktop.exe

Пять вкладок слева (подсвечивается активная):


=== Dashboard ===
Обзорная страница. Список Push Profile'ов. Выбери профиль чтобы
увидеть привязанные Build и Deploy настройки.


=== Build Config ===
Настройка сборки Unreal проекта.

Поля:
- Profile Name — любое имя для сохранения этого профиля
- UProject Path [кнопка ...] — путь к .uproject файлу.
  Инструмент прочитает EngineAssociation из него для определения версии движка.
- Custom Engine Path [кнопка ...] — если автоопределение движка не работает,
  укажи папку движка вручную. Например:
    C:\Program Files\Epic Games\UE_5.4
- Target Platform — Win64/Linux/Mac
- Build Configuration:
    Development — с отладочной информацией (для тестов)
    Shipping — оптимизированный финальный билд (для Steam)
    Debug / DebugGame / Test — специальные конфигурации
- Cook Content — обработать ассеты перед упаковкой. Включи для деплоя.
- Clean Build — удалить предыдущий вывод перед сборкой. Медленнее, но чище.
- Extra UAT Arguments — доп. флаги для RunUAT. Для продвинутых.
- Output Path Override — куда положить билд. По умолчанию Project/Saved/StagedBuilds.

Кнопки:
- Validate — проверить что .uproject и движок найдены.
  При ошибке покажет подробную диагностику.
- Copy — копирует текст ошибки в буфер обмена.
- Save / Delete / Clear — управление профилями.


=== Deploy Config ===
Настройка загрузки в Steam.

Поля:
- Profile Name — имя профиля деплоя
- Steam App ID — ID приложения из Steamworks.
  Где найти: Steamworks → главная страница приложения → "App ID".
  Или: Home > your app > Technical Tools > Edit Steamworks Settings.
- Depots [кнопка + Add Depot] — контейнеры контента в SteamPipe.
  Каждый депо = одна группа файлов (обычно один на платформу).
  Где найти Depot ID: Steamworks → App Admin → SteamPipe → Depots.
  - Depot ID — числовой ID депо
  - Content Root [кнопка ...] — локальная папка с файлами для загрузки
  - Local Path — маска файлов (обычно "*" — все)
  - Depot Path — путь в депо (обычно "." — корень)
  - Recursive — включать подпапки
- Branch Name — имя ветки SteamPipe:
    default — основной релиз
    beta — публичная бета
    staging / testing — внутреннее тестирование
  Где создать: Steamworks → App Admin → SteamPipe → Builds.
- Set live after upload — сделать билд активным сразу после загрузки
- Build Description — описание в истории билдов Steamworks
- Steam Account — какой аккаунт использовать.
  Управляется во вкладке Accounts.

Кнопки:
- Validate — проверить steamcmd, депо, аккаунт
- Copy — копировать ошибки
- Save / Delete / Clear


=== Push ===
Запуск полного цикла: сборка → деплой.

- Выпадающий список Push Profile (создаётся в init или вручную
  через привязку Build + Deploy)
- Start Push — запустить
- Cancel — отменить
- Прогресс-бар, текущий этап, логи в реальном времени


=== Accounts ===
Управление Steamworks аккаунтами.

- Username — логин от Steamworks (НЕ обычный Steam аккаунт)
- Password — пароль, хранится зашифрованным
- SSFN — Sentry File, кешируется после успешного логина.
  Если SSFN есть — повторный вход без Steam Guard кода.
- Login — проверяет креды. При первом входе запросит Steam Guard код:
  появится жёлтая панель "Steam Guard Code Required",
  введи 5-значный код из почты/приложения, нажми Submit.
- Logout — удаляет SSFN кеш
- Delete — удаляет аккаунт


Steam Guard
-----------
При первом логине без SSFN-кеша Steam требует код подтверждения.
В GUI появится отдельная панель с полем ввода. Введи код и нажми Submit.
После успешного входа SSFN сохраняется и код больше не нужен.


Engine Не Найден — Что Делать
-------------------------------
Если Validate в Build Config пишет "Engine not found":

1. Убедись что Unreal Engine установлен (лаунчерная или своя сборка).
2. Наведи курсор на "(?)" рядом с Custom Engine Path.
3. Нажми кнопку "..." справа от Custom Engine Path и выбери папку движка.
   Примеры:
   - Лаунчерная: C:\Program Files\Epic Games\UE_5.4
   - Собранная из исходников: D:\UnrealEngine (папка где лежит Engine/Build)
4. Нажми Validate снова — увидишь "Valid. Engine: 5.4.2 (Launcher)"

Инструмент ищет движок так:
- Читает EngineAssociation из .uproject (GUID или версия)
- Для GUID ищет в реестре Windows / манифестах Epic Games / папках Mac
- Для версий (например "5.4") ищет в C:\Program Files\Epic Games\UE_5.*


Где взять App ID и Depot ID
----------------------------
1. Зайди в https://partner.steamgames.com
2. Выбери своё приложение
3. App ID виден на главной странице приложения сверху
4. Depot ID: App Admin → SteamPipe → Depots → твой депо → ID


Логи
----
Логи пишутся в %APPDATA%/SteamUEDeployTool/logs/sdt-YYYYMMDD.log

Также в Push View логи видны в реальном времени.
Наведи на ошибку — текст можно выделить и скопировать (Ctrl+C).
Или нажми кнопку Copy рядом с сообщением.


Хранение данных
---------------
%APPDATA%/SteamUEDeployTool/

├── profiles/                # JSON профилей сборки и деплоя
│   ├── buildprofiles.json
│   ├── deploytargets.json
│   └── pushprofiles.json
├── accounts.json            # Список аккаунтов (без паролей)
├── ssfn/<account_id>/       # SSFN файлы после логина
├── credentials/<hash>.dat   # Зашифрованные пароли (DPAPI / AES)
└── logs/sdt-YYYYMMDD.log    # Файл лога


Сборка из исходников
--------------------
    git clone <repo>
    cd SteamUEDeployTool
    dotnet restore
    dotnet build
    dotnet test
    dotnet publish -c Release -o ./publish


Устранение проблем
------------------

"steamcmd not found"
    Установи SteamCMD и добавь в PATH.

"Engine not found"
    Укажи Custom Engine Path вручную (см. раздел выше).

"RunUAT not found"
    Повреждённая установка движка. Переустанови движок.

"Steam Guard code required / incorrect"
    Проверь почту или Steam мобильное приложение.
    Введи актуальный 5-значный код.
    Код действует ~30 секунд.

"No saved password"
    Удали аккаунт и добавь заново с паролем.

"Build failed with exit code N"
    Проверь логи в Push View или файл лога.
    Частые причины: ошибки компиляции, несовместимая версия движка,
    закончилось место на диске, неправильный путь.

"Deploy failed"
    Проверь что SteamCMD работает: запусти steamcmd.exe отдельно.
    Проверь что аккаунт имеет права на загрузку в это App ID.
    Проверь VDF файлы (временные, генерируются в %TEMP%/SteamUEDeployTool/).
