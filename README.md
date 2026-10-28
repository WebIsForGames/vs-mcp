# vs-mcp: отладка C++ в Visual Studio из Claude Code

MCP-сервер и skill, через которые Claude управляет отладчиком уже запущенной Visual Studio 2022 или 2026. Claude может поставить breakpoint, запустить отладку, дождаться остановки, сообщить место и причину и продолжить выполнение.

Сервер подключается к VS через COM-автоматизацию EnvDTE (Running Object Table). Расширение в VS ставить не нужно: вы работаете в своей обычной VS с открытым решением, и breakpoint-ы Claude появляются прямо в редакторе.

## Требования

- Windows, Visual Studio 2022 (17.x) и/или 2026 (18.x) с C++ workload.
- .NET 10 SDK для сборки; для запуска достаточно .NET 10 Runtime.
- Claude Code.
- VS и Claude Code должны работать с одинаковыми правами. VS, запущенная от администратора, не видна неэлевированному серверу.

## Сборка

```powershell
.\build.ps1
```

Скрипт публикует сервер в `plugin/server/`, откуда его запускает плагин.

## Установка

### Как плагин (сервер и skill вместе)

В Claude Code:

```
/plugin marketplace add O:\Personal\Projects\vs-mcp
/plugin install vs-cpp-debug@vs-mcp
```

Claude Code копирует плагин в свой кэш, поэтому сначала выполните `.\build.ps1`. После пересборки обновите плагин: `/plugin marketplace update vs-mcp`, затем переустановите его.

Чтобы попробовать без установки, запустите Claude Code с плагином только на одну сессию:

```bash
claude --plugin-dir O:\Personal\Projects\vs-mcp\plugin
```

### Вручную

```bash
claude mcp add --scope user vs-debug -- O:\Personal\Projects\vs-mcp\plugin\server\VsDebugMcp.exe
```

Затем скопируйте папку `plugin/skills/vs-cpp-debug` в `%USERPROFILE%\.claude\skills\`.

## Инструменты

| Инструмент | Что делает |
|---|---|
| `vs_list_instances` | Запущенные VS 2022/2026: pid, версия, решение, режим отладчика |
| `vs_select_instance` | Выбор экземпляра, когда их несколько. Если VS одна, она выбирается автоматически |
| `vs_set_breakpoint` | Breakpoint по абсолютному пути и строке, с необязательным C++ условием |
| `vs_remove_breakpoint` | Удаление breakpoint-а на строке |
| `vs_list_breakpoints` | Все breakpoint-ы, включая состояние привязки (`bindState`, `boundLines`) |
| `vs_start_debugging` | Сборка и запуск отладки (F5). При ошибке сборки возвращает ошибки компилятора |
| `vs_wait_for_break` | Ждёт остановку (до 300 с). Возвращает причину, файл, строку, функцию, или `timedOut`, или сообщает, что программа завершилась |
| `vs_continue` | Продолжение из режима break |
| `vs_get_state` | Текущий режим и место остановки |
| `vs_stop_debugging` | Остановка отладки (Shift+F5) |

Режимы отладчика: `design` (отладка не идёт), `run`, `break`.

## Как пользоваться

Откройте решение в VS и попросите Claude, например: «Поставь breakpoint в начале Square в main.cpp, запусти и скажи, с какими значениями она вызывается». Порядок работы описан в skill [`vs-cpp-debug`](plugin/skills/vs-cpp-debug/SKILL.md).

## Проверочный проект

`samples/BreakpointDemo` — маленькое консольное C++ приложение. Toolset выбирается по версии VS: v143 в VS 2022, v145 в VS 2026. На нём проверены обе версии VS:

- breakpoint и условный breakpoint;
- breakpoint на пустой строке (VS привязывает его к следующей строке с кодом);
- повторные срабатывания в цикле;
- ошибка сборки;
- таймаут ожидания;
- остановка отладки;
- выбор между двумя запущенными экземплярами.

## Ограничения

- **Модальные диалоги VS блокируют автоматизацию.** Сервер сообщает «busy and rejected the call»; закройте диалог в VS.
- **Сборка перед запуском.** С `build: false` устаревший проект вызовет в VS диалог «out of date».
- **Что не входит в MVP:** просмотр переменных, вычисление выражений, call stack, шаги, attach к процессу.

## Структура

```
src/VsDebugMcp/          MCP-сервер (C#, .NET 10, ModelContextProtocol SDK)
  Com/                   STA-поток, OLE message filter, Running Object Table
  VisualStudio/          поиск экземпляров VS, операции отладчика
  Tools/                 MCP-инструменты
tests/VsDebugMcp.Tests/  unit-тесты (без VS)
samples/BreakpointDemo/  C++ проект для ручной проверки
plugin/                  плагин Claude Code: skill, .mcp.json, server/ после сборки
Plans/                   планы
```

Тесты:

```bash
dotnet test VsDebugMcp.slnx
```
