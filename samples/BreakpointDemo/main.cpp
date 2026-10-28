#include <cstdio>

static int Square(int value)
{
    int result = value * value;
    return result;
}

int main()
{
    int sum = 0;
    for (int i = 1; i <= 3; ++i)
    {
        sum += Square(i);
    }

    std::printf("sum = %d\n", sum);
    return 0;
}
