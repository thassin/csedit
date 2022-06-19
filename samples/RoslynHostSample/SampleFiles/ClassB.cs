using System; // not needed.

namespace SampleFiles
{
    public class ClassB
    {
        public void MethodB()
        {
            string name = "Joe";
            var a = new ClassA();
            a.MethodA( name );
        }
    }
}

