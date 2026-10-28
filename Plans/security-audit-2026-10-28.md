# Security audit — 2026-10-28

1. Inspect all source, plugin configuration, build scripts and dependency manifests without executing the server or restoring packages.
2. Trace transport, COM capabilities, untrusted input and data returned to the MCP client.
3. Inspect available resolved dependency metadata and package sources; distinguish verified code facts from unverified runtime and supply-chain behavior.
4. Report findings, internal-network suitability and mitigations with source references.

## Результат аудита

Проверено 2026-10-28: HEAD 4ca6bef514cd1d5f56445dcce8f6b1e9ec3a67e8 и текущее рабочее дерево. Пользовательские изменения README.md и samples/BreakpointDemo/main.cpp сохранены. Просмотрены все исходники сервера, тесты, sample, build.ps1, скрытые конфигурации плагина и локального marketplace, project.assets.json и опубликованный deps.json.

Вывод: условно пригоден как локальный инструмент разработчика для доверенных решений и доверенного MCP-клиента. Не является изолированной песочницей. Для строгого требования отсутствия исходящих соединений нужна доработка обработки путей и сетевые ограничения окружения.

### Сеть и зависимости

- Program.cs:21 использует WithStdioServerTransport. TCP/UDP/HTTP-слушателей, удаленного COM-хоста, HTTP-клиента, загрузчика кода и самообновления в исходниках не найдено. COM ROT подключает уже работающую локальную Visual Studio.
- plugin/.mcp.json запускает локальный server/VsDebugMcp.exe без аргументов. Marketplace указывает ./plugin. Установочных hooks и скачивания из GitHub/npm в репозитории нет.
- build.ps1:6 выполняет dotnet publish с неявным restore. Текущие источники: nuget.org и Microsoft Visual Studio Offline Packages. На другой машине источники могут отличаться.
- Три прямые зависимости сервера: Microsoft.Extensions.Hosting 10.0.14, Microsoft.VisualStudio.Interop 17.14.40260, ModelContextProtocol 2.3.0. В разрешенном графе 34 NuGet-пакета; metadata локального кеша для всех 34 указывает https://api.nuget.org/v3/index.json. Это сведения кеша, а не независимая проверка происхождения бинарников.
- Транзитивные зависимости включают ModelContextProtocol.Core, Microsoft.Extensions.*, Microsoft.Extensions.AI.Abstractions, Microsoft.VisualStudio.Imaging.Interop.14.0.DesignTime и System.Diagnostics.EventLog. Наличие AI.Abstractions само по себе не означает вызовы облачной модели; таких вызовов в прикладном коде нет.
- Тесты дополнительно используют coverlet.collector 6.0.4, Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, xunit.runner.visualstudio 3.1.4.
- dotnet list VsDebugMcp.slnx package --vulnerable --include-transitive --no-restore успешно выполнен: оба проекта без известных уязвимых пакетов по текущим источникам NuGet. Первый запуск был ограничен сетевой песочницей; повторный с разрешенным доступом завершился с кодом 0. Отсутствие известных advisory не доказывает отсутствие неизвестных дефектов или вредоносного содержимого.

### Замечания по риску

1. Высокий при недоверенных аргументах MCP: сетевой путь вызывает исходящий доступ.
   SourcePath.cs:9-14 принимает любой полностью квалифицированный путь, включая UNC и extended UNC. DebuggerService.cs:25 вызывает File.Exists до разрешения экземпляра VS. Поэтому достаточно вызвать vs_set_breakpoint с сетевым адресом даже без запущенной VS. Возможны DNS/SMB-запросы и попытка Windows-аутентификации на чужом сервере; раскрытие NTLM challenge-response зависит от политик Windows, firewall и доступности ресурса. Это не выдача пароля открытым текстом и не входящий сетевой порт.
   Подтверждено без сетевого доступа: Path.IsPathFullyQualified и Path.GetFullPath принимают \\audit.invalid\share\file.cpp и \\?\UNC\audit.invalid\share\file.cpp. File.Exists на этих адресах намеренно не вызывался.
   Рекомендация: ограничить разрешенные корни исходников; отклонять непредусмотренные UNC/device-пути до файлового ввода-вывода. Если используются сетевые диски, явно разрешать нужные серверы/ресурсы, учитывать mapped drives и reparse points. Ограничивать SMB на уровне ОС до доверенных адресов.

2. Существенная граница доверия: сборка и запуск с правами Visual Studio.
   DebuggerService.cs:106 вызывает синхронную сборку, :114 запускает startup project. Код проекта, build events и MSBuild tasks способны выполнять команды и обращаться в сеть. Ограничений по решению/каталогу в сервере нет; доступен выбор видимых экземпляров VS. Это штатная возможность отладчика, а не обнаруженная скрытая закладка.
   Рекомендация: доверенные решения, обычные права пользователя, разрешения на изменяющие инструменты у MCP-клиента; для недоверенных проектов отдельная VM. Сетевые настройки и запускаемые проекты VS рассматриваются отдельно от сервера.

3. Конфиденциальность: результаты MCP содержат внутренние данные.
   VsSession.cs:73 возвращает полный путь решения; DebuggerSnapshot возвращает имена процесса, функции, модуля и файла; DebuggerService.cs:243 возвращает условия breakpoint; :261 возвращает текст ошибок компиляции. Нет фильтрации секретов или ограничения области решения. Сам сервер отдает эти данные по stdio; последующая отправка провайдеру модели зависит от MCP-клиента и его конфигурации. Полного чтения исходников или памяти инструментами этого сервера нет. Skill дополнительно предлагает клиенту прочитать исходник собственными средствами.
   Рекомендация: использовать одобренный корпоративный клиент/провайдера и правила обращения с исходниками. Текст диагностик считать недоверенными данными, а не инструкциями агенту.

4. Средний, supply chain: нет репозиторного NuGet.Config, packages.lock.json и global.json.
   Прямые версии объявлены, но весь граф и разрешенные источники не закреплены в репозитории. Обычная запись Version задает минимальную версию NuGet, а не строгий locked restore. build.ps1 использует настройки окружения и выполняет неявный restore.
   Рекомендация: утвержденный NuGet feed/зеркало, package source mapping, lock-файл и restore --locked-mode в CI; publish --no-restore после контролируемого restore. Публикуемую папку plugin/server и кеш плагина защищать от записи посторонними; соответствие существующих DLL исходникам этим аудитом не доказано.

5. Низкий/средний, доступность: отмена не прерывает заблокированный COM-вызов.
   StaComDispatcher.cs:11 содержит неограниченную очередь; InvokeAsync не принимает cancellation token. Синхронная сборка выполняется раньше запуска StartTimeout. Таймауты polling не ограничивают зависший COM-вызов. Следствие: зависание MCP/накопление запросов при недоверенном клиенте или зависшей VS.
   Рекомендация: ограничить очередь, отменять еще не начатые операции и использовать внешний watchdog для зависшего процесса.

Дополнительно: condition передается в Breakpoints.Add без ограничений выражения (DebuggerService.cs:44). Его семантика определяется отладчиком, поэтому условие нельзя считать гарантированно без побочных эффектов. Произвольное исполнение через конкретное условие не воспроизводилось и как подтвержденная уязвимость не заявляется.

### Границы проверки

Сервер и отлаживаемые проекты не запускались, сборка/restore не выполнялись, сетевой трафик процессов не снимался. Unit-тесты прочитаны, но они не проверяют сетевую изоляцию; запускать их для утверждения о сетевой безопасности было бы недостаточно. Исходники всех сторонних пакетов и бинарное соответствие plugin/server не проверялись. Существующие Windows-службы RPC/SMB, Visual Studio, расширения IDE и MCP-клиент могут иметь самостоятельную сетевую активность.

### Первичные внешние источники

- MCP SDK: https://www.nuget.org/packages/ModelContextProtocol/2.3.0
- Windows SMB NTLM blocking: https://learn.microsoft.com/en-us/windows-server/storage/file-server/smb-ntlm-blocking
- NuGet lock files: https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files
- NuGet source mapping: https://learn.microsoft.com/en-gb/nuget/consume-packages/package-source-mapping

Все четыре шага плана завершены. Изменения функционального кода не вносились.
