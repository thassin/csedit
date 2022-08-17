using System;

namespace TestProject02b
{
    public class MyClass
    {
        public int MyInt { get; set; }
        public string MyString { get; /*internal*/ set; }

        public MyClass() {
            MyInt = 42;
            MyString = "Hello!";
        }
    }
}
