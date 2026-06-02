ent-ProtoCore = Протоядро
    .desc = Стационарное имперское хранилище энергии. Используется для поддержания аварийных цепей питания и резервных контуров станции.
ent-ProtoCoreConsole = консоль управления Протоядром
    .desc = Защищенный терминал, подключенный к аварийному контуру управления Протоядра.
ent-ProtoCoreActivationKey = ключ активации
    .desc = Защищенный имперский ключ данных, предоставляющий доступ к аварийным протоколам Протоядра.
ent-AshHackingDevice = пепельный взламывающий аппарат
    .desc = Компактный взламывающий аппарат для внешней авторизации в системе Протоядра.
ent-PinpointerProtoCoreActivationKey = трекер ключа активации
    .desc = Ручной трекер, настроенный на аварийный маячок ключа активации.

ash-legion-title = Легион Пепла
ash-legion-description = Легион Пепла использует встроенное Протоядро станции вместо доставки ядерной боеголовки.
ash-legion-welcome =
    Вы - Легион Пепла.
    Проникните на {$station}, захватите ключ активации, подключите пепельный взламывающий аппарат к консоли Протоядра и запустите процедуру плавления ядра.
ash-legion-briefing = Захватите ключ активации, взломайте консоль Протоядра, начните плавление и удерживайте ядро до завершения отсчета.

roles-antag-ash-legion-name = оперативник Легиона Пепла
roles-antag-ash-legion-medic-name = медик Легиона Пепла
roles-antag-ash-legion-commander-name = командир Легиона Пепла
roles-antag-ash-legion-objective = Используйте ключ активации и пепельный взламывающий аппарат, чтобы запустить процедуру плавления Протоядра.

proto-core-announcement-sender = аварийная система Протоядра
proto-core-announcement-unauthorized-connection = Внимание. Зафиксировано несанкционированное подключение к Протоядру. Обнаружена попытка внешней авторизации.
proto-core-announcement-meltdown-started = Процедура плавления Протоядра инициирована. До перехода ядра в критическое состояние: {$time}.
proto-core-announcement-stabilized = Стабилизация завершена. Протоядро возвращено в режим аварийного накопления энергии.
proto-core-announcement-critical = Протоядро перешло в критическое состояние. Автоматический вызов эвакуационного шаттла инициирован.
proto-core-announcement-shuttle-called = Эвакуационный шаттл вызван автоматически из-за критического состояния Протоядра.

proto-core-console-key-accepted = Ключ активации авторизует аварийный канал Протоядра.
proto-core-console-hack-start = Пепельный взламывающий аппарат начинает принудительное подключение.
proto-core-console-device-already-connected = Взламывающий аппарат уже заблокирован в контуре Протоядра.
proto-core-console-device-disconnected = Взламывающий аппарат выведен из контура Протоядра.
ash-hacking-device-locked = Аппарат заблокирован в контуре Протоядра. Отключение возможно только через консоль управления.

proto-core-verb-start = Запустить процедуру плавления
proto-core-verb-stabilize = Стабилизировать Протоядро
proto-core-verb-physical-override = Принудительно отключить аппарат

proto-core-state-idle = ожидание
proto-core-state-hacked = внешнее подключение
proto-core-state-meltdown = плавление
proto-core-state-critical = критическое состояние
proto-core-state-stabilized = стабилизировано
proto-core-examine-status = Состояние: {$state}. Оставшееся время: {$time}.
proto-core-console-examine = Авторизация: {$authorized}. Взламывающий аппарат: {$device}.

ash-legion-roundend-critical = Протоядро достигло критического состояния.
ash-legion-roundend-stabilized = Протоядро было стабилизировано.
ash-legion-roundend-started = Легион Пепла активировал процедуру плавления Протоядра.
steal-target-groups-proto-core-activation-key = ключ активации
