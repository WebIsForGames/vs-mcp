# Ожидание исключений, __debugbreak и assert

Цель: `vs_wait_for_break` и `vs_continue` понятно сообщают не только об остановках на breakpoint-ах, но и об исключениях (например, разыменование nullptr), `__debugbreak()`, `DebugBreak()`, `__asm int 3` и `assert(false)`. Для каждой остановки нужны причина, текст исключения и строка в коде пользователя.

## Этап 0. Проверка допущений на живой VS 2022 и 2026

Проверочный проект `samples/ExceptionDemo`: сценарий выбирается аргументом командной строки. Одноразовый probe в scratchpad управляет VS через DTE так же, как сервер: STA-поток, ROT, тот же `StaComDispatcher`.

Проверить:

1. Какие `LastBreakReason` и текущий кадр дают разыменование nullptr, `__debugbreak()`, `DebugBreak()`, `__asm int 3` (x86), необработанный и перехваченный `throw`, `assert(false)`.
2. Останавливается ли VS по умолчанию на access violation и на перехваченном C++-исключении (Exception Settings).
3. Доходят ли события `DebuggerEvents` (`OnEnterBreakMode`, `OnExceptionThrown`, `OnExceptionNotHandled`) до STA-потока, который ждёт очередь `BlockingCollection`, и что в них приходит: тип, имя, код, описание.
4. Привязывается ли function breakpoint на `_wassert` и останавливает ли он программу до диалога CRT.
5. Отличается ли состояние между повторными остановками `__debugbreak` в цикле.
6. Не открывает ли VS модальных диалогов на этих остановках.

Результаты записываются ниже. Этапы 1–5 уточняются по ним.

## Этап 1. Место остановки в коде пользователя

Если у текущего кадра нет исходника (остановка на function breakpoint в CRT, например `_wassert`), `DebuggerSnapshot` идёт по стеку текущего потока до первого кадра с файлом и строкой. Для исключений и `int 3` VS выбирает такой кадр сама. `location` — этот кадр, верхний кадр (функция, модуль) — отдельное поле. Выбор кадра — чистая функция с unit-тестами.

## Этап 2. Текст исключения и счётчик остановок

Подписка на `DebuggerEvents` целевого экземпляра (`OnEnterBreakMode`, `OnEnterRunMode`) и счётчик этих переходов. Текст исключения — новые строки панели Debug окна Output с прошлого отчёта, с ожиданием до 1 с: `OnExceptionThrown` и `OnExceptionNotHandled` не приходят. `DebuggerState` получает поле с этим текстом. Счётчик участвует в сравнении состояний в `vs_continue`, чтобы повторная остановка на том же месте не выглядела зависанием.

## Этап 3. assert

Function breakpoint на `_wassert`, чтобы остановка была до диалога CRT. `vs_set_breakpoint` получает параметр `function` вместо `file` и `line`, SKILL.md советует ставить `_wassert`, когда важен assert. После такой остановки `continue` открывает диалог, это тоже описывается в SKILL.md.

## Этап 4. Exception Settings (по желанию)

Инструмент для остановки в момент `throw` C++-исключения или Win32-исключения с заданным кодом, а не только на необработанных. Через `EnvDTE90.Debugger3.ExceptionGroups`.

## Этап 5. Документация и проверка

SKILL.md, README.md и README.ru.md. В SKILL.md: после необработанного access violation не продолжать, а останавливать отладку, потому что `continue` повторяет исключение. Ручная проверка всех сценариев `ExceptionDemo` в VS 2022 и 2026 через MCP-инструменты.

## Результаты проверки

Проверено 2026-10-29 в VS 2022 17.14 и VS 2026 18.10 на `samples/ExceptionDemo`, x64 и x86. Обе версии ведут себя одинаково.

| Сценарий | `LastBreakReason` | Текущий кадр | После `continue` |
|---|---|---|---|
| Разыменование nullptr | exceptionThrown, затем exceptionNotHandled | строка разыменования | пара thrown / not handled повторяется бесконечно: инструкция выполняется заново |
| nullptr в `__try`/`__except` | exceptionThrown | строка разыменования | исключение перехвачено, программа завершается |
| `__debugbreak()` в цикле | exceptionThrown | строка `__debugbreak` | следующая остановка с тем же состоянием |
| `DebugBreak()` | exceptionThrown | строка вызова; верхний кадр потока в KernelBase | программа продолжается |
| `__asm int 3` (x86) | exceptionThrown | строка `__asm` | программа продолжается |
| Необработанный `throw` | exceptionNotHandled | строка `throw`; верхний кадр в KernelBase | программа завершается |
| Перехваченный `throw` | остановки нет | | |
| `assert(false)` | остановки нет: VS в run, в программе диалог CRT | | |
| `assert(false)` и function breakpoint `_wassert` | breakpoint, `BreakpointLastHit.FunctionName` = `_wassert`, файла нет | ucrtbased.dll без исходника, строка `assert` во втором кадре | диалог CRT |

По пунктам:

1. Для исключений и `int 3` VS сама делает текущим первый кадр с исходником. Обход стека нужен только для остановки на function breakpoint без исходника.
2. По умолчанию VS останавливается при throw на access violation, invalid handle, Assertion failed (0xC0000420), Ctrl+C, Ctrl+Break и ошибках sanitizer. На `std::` исключениях — только если они не обработаны.
3. `OnEnterBreakMode`, `OnEnterRunMode` и `OnEnterDesignMode` приходят на каждый переход, на отдельном RPC-потоке, а не на STA: прокачка сообщений не нужна. `OnExceptionThrown` и `OnExceptionNotHandled` не пришли ни разу, `$exception` в native-коде не определена. Текст исключения есть только в панели Debug окна Output и появляется через 0,07–0,4 с после перехода в break. Примеры: «Exception thrown: read access violation. **pointer** was nullptr.», «A breakpoint instruction (__debugbreak() statement or a similar call) was executed in ExceptionDemo.exe.», «Microsoft C++ exception: std::runtime_error at memory location …». Текст зависит от языка интерфейса VS.
4. `_wassert` привязывается без символов (4 привязки в ucrtbased.dll) и останавливает программу до диалога.
5. Повторные `__debugbreak` дают одинаковое состояние, текущий `vs_continue` принял бы следующую остановку за ту же. `OnEnterBreakMode` приходит на каждую.
6. Модальных диалогов VS не было. Вызовы DTE в break занимали 35–900 мс.

Дополнительно: `vs_start_debugging` ждёт выхода из design опросом раз в 50 мс, поэтому программа, которая успевает завершиться быстрее, выглядит незапустившейся. Счётчик переходов из этапа 2 решает и это.

## Реализация

Этапы 1, 2, 3 и 5 выполнены 2026-10-29, этап 4 не делался. Все сценарии `ExceptionDemo`, а также line breakpoint и ошибки в аргументах `vs_set_breakpoint`, проверены через MCP-инструменты в VS 2022 и VS 2026. Повторные `__debugbreak` теперь возвращаются из `vs_continue` как новые остановки. Не проверен путь «программа завершилась до первого опроса»: ни один сценарий не завершается быстрее 50 мс.
