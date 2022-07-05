using System;
using TestProject02b;

namespace TestProject02
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            MyClass test = new MyClass();
            test.MyInt = 100;
            test.MyString = "Bye!";
        }
    }
}
