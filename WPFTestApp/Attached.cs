using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace WPFTestApp
{
    public class Verifier
    {

    }

    public class Attached
    {



        public static Verifier GetVerifier(DependencyObject obj)
        {
            return (Verifier)obj.GetValue(VerifierProperty);
        }

        public static void SetVerifier(DependencyObject obj, Verifier value)
        {
            obj.SetValue(VerifierProperty, value);
        }

        // Using a DependencyProperty as the backing store for Verifier.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty VerifierProperty =
            DependencyProperty.RegisterAttached("Verifier", typeof(Verifier), typeof(Attached), new PropertyMetadata(null));



    }
}
