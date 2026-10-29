#include <windows.h>

#include <cassert>
#include <cstdio>
#include <cstring>
#include <stdexcept>

// Each scenario stops the debugger in its own way. Pass the scenario name as the command argument.

static int ReadThroughNull()
{
    int* pointer = nullptr;
    return *pointer;
}

static int ReadThroughNullHandled()
{
    __try
    {
        return ReadThroughNull();
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return -1;
    }
}

static void Throw()
{
    throw std::runtime_error("demo failure");
}

int main(int argc, char** argv)
{
    const char* scenario = argc > 1 ? argv[1] : "";
    if (strcmp(scenario, "null") == 0)
    {
        std::printf("%d\n", ReadThroughNull());
    }
    else if (strcmp(scenario, "null-handled") == 0)
    {
        std::printf("handled: %d\n", ReadThroughNullHandled());
    }
    else if (strcmp(scenario, "debugbreak") == 0)
    {
        for (int i = 1; i <= 3; ++i)
        {
            __debugbreak();
            std::printf("after break %d\n", i);
        }
    }
    else if (strcmp(scenario, "debugbreak-api") == 0)
    {
        DebugBreak();
    }
    else if (strcmp(scenario, "int3") == 0)
    {
#if defined(_M_IX86)
        __asm int 3
#else
        std::printf("__asm is available only in x86 builds\n");
#endif
    }
    else if (strcmp(scenario, "throw") == 0)
    {
        Throw();
    }
    else if (strcmp(scenario, "throw-caught") == 0)
    {
        try
        {
            Throw();
        }
        catch (const std::exception& error)
        {
            std::printf("caught: %s\n", error.what());
        }
    }
    else if (strcmp(scenario, "assert") == 0)
    {
        assert(false);
    }
    else
    {
        std::printf("Usage: ExceptionDemo null|null-handled|debugbreak|debugbreak-api|int3|throw|throw-caught|assert\n");
        return 1;
    }

    std::printf("%s: done\n", scenario);
    return 0;
}
