using System;
using Newtonsoft.Json;

namespace TestProject04b
{
    public class MyClass
    {
        public int MyInt { get; set; }
        public string MyString { get; /*internal*/ set; }

        public MyClass() {
            MyInt = 42;
            MyString = "Hello!";
        }

        public string Serialize() {
            string json = JsonConvert.SerializeObject( this );
            return json;
        }
    }
}
