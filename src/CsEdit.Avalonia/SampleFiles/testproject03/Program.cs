using System;
using Newtonsoft.Json;

namespace TestProject03
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            Program prog = new Program();
            string json = JsonConvert.SerializeObject( prog );
        }
    }
}
