using System; // not needed.

namespace SampleFiles
{
    public class ClassD
    {
        public void MethodX()
        {
            string name = "Joe";

            var c = new ClassC();
            c.MethodA( name );
            c.MethodA( "sum is " + c.MethodB( 1, 2 ) );
        }
    }
}
