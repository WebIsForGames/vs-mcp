# План: MCP-сервер и skill для отладки C++ в Visual Studio 2022/2026

## Цель

Дать Claude возможность управлять отладчиком уже запущенной Visual Studio (2022 и 2026) с открытым C++ решением.

Минимальный функционал (MVP):

- поставить breakpoint (файл + строка, необязательное условие);
- дождаться его срабатывания (с таймаутом) и узнать, где остановились;
- продолжить выполнение.

## Выбранный подход

Управление VS снаружи через COM-автоматизацию **EnvDTE** (Running Object Table).

- Одна и та же модель автоматизации есть в обеих версиях: `VisualStudio.DTE.17.0` (VS 2022) и `VisualStudio.DTE.18.0` (VS 2026). На этой машине обе зарегистрированы (VS 2022 17.14.38, VS 2026 18.10.2).
- Ничего не нужно ставить в саму VS: пользователь работает в своей обычной VS, MCP-сервер подключается к ней.
- Нужные API: `Debugger.Breakpoints.Add`, `Debugger.Go`, `Debugger.Stop`, `Debugger.CurrentMode`, `Debugger.LastBreakReason`, `Debugger.BreakpointLastHit`, `Debugger.CurrentStackFrame` (`StackFrame2.FileName/LineNumber`), `Solution.SolutionBuild`.

Почему не другие варианты:

- **Расширение VSIX со встроенным MCP-сервером** — надо собирать и ставить отдельно под каждую версию VS. Для MVP это лишнее, можно вернуться к нему позже.
- **Прямой DAP/vsdbg** — это уже не отладка в VS, и у vsdbg лицензионные ограничения.
- **Python + pywin32** — нужен дополнительный runtime, COM типизирован слабее. В C# поддержка COM встроенная, и есть официальный MCP C# SDK.

## Стек

- C# / .NET 10 (`net10.0-windows`, LTS; SDK 10.0.401 установлен).
- `ModelContextProtocol` 2.3.0 (официальный C# SDK), транспорт stdio.
- `Microsoft.VisualStudio.Interop` 17.14.40260 (EnvDTE, EnvDTE80, EnvDTE90a). Интерфейсы EnvDTE стабильны, поэтому те же сборки подходят и для VS 2026. Совместимость с net10 проверить на шаге 1.
- Тесты: xUnit.

## Архитектура

```
Claude Code ──stdio/MCP──> VsDebugMcp.exe ──COM (ROT)──> devenv.exe (2022 / 2026)
```

Компоненты `src/VsDebugMcp`:

- `Program.cs` — хост MCP (stdio). Все логи пишутся в stderr, stdout отдан под протокол.
- `Com/StaComDispatcher` — отдельный STA-поток, через который выполняются все вызовы DTE.
- `Com/MessageFilter` — OLE `IMessageFilter`: повторяет вызов, если VS занята (`RPC_E_CALL_REJECTED`, `SERVERCALL_RETRYLATER`).
- `VisualStudio/VsInstanceLocator` — перебирает ROT, разбирает моникеры `!VisualStudio.DTE.<17|18>.0:<pid>` и выбирает экземпляр.
- `VisualStudio/DebuggerService` — операции отладчика и снимок состояния (режим, причина остановки, место остановки).
- `Tools/*Tools.cs` — MCP-инструменты, тонкие обёртки над `DebuggerService`.

Выбор экземпляра VS: если запущен ровно один экземпляр, он выбирается автоматически. Если их несколько, инструмент возвращает ошибку со списком экземпляров, и нужно вызвать `vs_select_instance`.

## MCP-инструменты (MVP)

| Инструмент | Что делает |
|---|---|
| `vs_list_instances` | Запущенные VS: pid, версия (2022/2026), открытое решение, режим отладчика |
| `vs_select_instance(pid)` | Выбор экземпляра для дальнейших вызовов |
| `vs_set_breakpoint(file, line, condition?)` | Ставит breakpoint и возвращает его данные (файл, строка, enabled, привязан ли) |
| `vs_remove_breakpoint(file, line)` | Удаляет breakpoint |
| `vs_list_breakpoints` | Список breakpoint-ов |
| `vs_start_debugging(build = true)` | Синхронно собирает решение. Если сборка упала, возвращает ошибку, а не модальный диалог VS. Затем запускает отладку, `Debugger.Go(false)` |
| `vs_wait_for_break(timeout_sec = 60)` | Опрашивает `CurrentMode` раз в ~200 мс. Возвращает `break` (причина, файл/строка/функция, сработавший BP), `running` + `timedOut`, или `design` (процесс завершился). Таймаут ограничен сверху (300 с), чтобы не упереться в таймаут вызова MCP. Skill вызывает инструмент в цикле |
| `vs_continue` | `Debugger.Go(false)` из режима break. Возвращает управление только после того, как VS реально вышла из break, чтобы следующий `wait_for_break` не поймал старую остановку |
| `vs_get_state` | Текущий режим и место остановки |
| `vs_stop_debugging` | `Debugger.Stop`. Нужен для уборки за собой |

Вне MVP, отдельным этапом: locals, вычисление выражений, call stack, шаги (over/into/out), Break All, attach к процессу, потоки, исключения.

## Skill `vs-cpp-debug`

`SKILL.md` описывает рабочий процесс:

1. `vs_list_instances`, при необходимости выбор экземпляра. Если VS не запущена, попросить пользователя открыть решение.
2. Breakpoint ставится по абсолютному пути и на исполняемую строку. Отладка в конфигурации Debug, иначе breakpoint может не привязаться.
3. Запуск: `vs_start_debugging`, или `vs_continue`, если отладка уже идёт.
4. Цикл `vs_wait_for_break` с разумными таймаутами. Сообщать пользователю, где остановились и по какой причине.
5. `vs_continue` или `vs_stop_debugging`.
6. Уборка: удалить только те breakpoint-ы, которые поставил сам. Не останавливать чужую сессию отладки без вопроса.

Плюс разбор типичных ситуаций: breakpoint не привязан (нет PDB или не та конфигурация), процесс завершился до остановки, остановка по исключению вместо breakpoint, VS занята.

## Упаковка и установка

Плагин Claude Code в каталоге `plugin/`: `.claude-plugin/plugin.json`, `.mcp.json` (запуск `${CLAUDE_PLUGIN_ROOT}/server/VsDebugMcp.exe`), `skills/vs-cpp-debug/SKILL.md`. В корне репозитория лежит `.claude-plugin/marketplace.json` для локальной установки:

```
/plugin marketplace add O:\Personal\Projects\vs-mcp
/plugin install vs-cpp-debug
```

Скрипт `build.ps1` выполняет `dotnet publish` в `plugin/server/`. В README также описан ручной вариант: `claude mcp add` и копирование skill в `~/.claude/skills/`.

## Структура репозитория

```
Plans/vs-cpp-debug-mcp.md
src/VsDebugMcp/                 C# MCP-сервер
tests/VsDebugMcp.Tests/         unit-тесты (без VS)
samples/BreakpointDemo/         маленький C++ консольный проект (.sln + .vcxproj) для проверки
plugin/                         плагин Claude Code (skill + .mcp.json + server/ после сборки)
.claude-plugin/marketplace.json
build.ps1
README.md
```

## Этапы реализации

1. Каркас: решение, проект .NET 10, MCP stdio-хост, проверка, что пакеты interop работают под net10.
2. STA-диспетчер, message filter, локатор ROT; инструменты `vs_list_instances` и `vs_select_instance`.
3. Инструменты breakpoint-ов.
4. Инструменты исполнения: start, continue, wait_for_break, get_state, stop.
5. `samples/BreakpointDemo` и сквозная проверка в VS 2022 и VS 2026: поставить BP, запустить, дождаться, продолжить, дождаться завершения.
6. Skill, плагин, `build.ps1`, README.
7. Unit-тесты: разбор моникеров, отображение режимов и причин остановки, ограничение таймаутов, нормализация путей. Финальный просмотр diff.

## Риски

- **Модальные диалоги VS блокируют автоматизацию** (ошибки сборки, нет отладочной информации). Решение: сборка до `Go` с проверкой `LastBuildInfo`, понятные сообщения об ошибках.
- **VS запущена от администратора.** Неэлевированный сервер не видит её в ROT. Описать в README и skill.
- **VS занята, COM-вызов отклонён.** Решение: message filter.
- **Гонка смены режимов после `Go`.** Решение: `vs_continue` дожидается выхода из break.
- **Breakpoint на неисполняемой строке.** VS перенесёт его на ближайшую исполняемую строку. Сообщать фактическую строку из привязанных дочерних breakpoint-ов.
- **Таймаут вызова MCP-инструмента.** Решение: ограниченное ожидание и цикл в skill.
- **Toolset sample-проекта.** В VS 2026 на этой машине есть только v145, поэтому проект использует `$(DefaultPlatformToolset)`: v143 в VS 2022 и v145 в VS 2026, без retarget.
