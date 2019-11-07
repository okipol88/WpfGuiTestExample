using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WPFTestApp.Styling;

namespace WPFTestApp.UnitTests
{
    public class GUIThread<T>
 where T : FrameworkElement
    {
        private readonly Thread _thread;

        private T _element;
        private Func<Application, Window, T> _elementProvider;
        private Application _app;
        private readonly ManualResetEventSlim _manualResetEventSlim = new ManualResetEventSlim();
        private Dispatcher _dispatcher;
        private Window _window;

        public GUIThread(Func<Application, Window, T> elementProvider)
        {
            _thread = new Thread(OnThreadStart);
            _thread.SetApartmentState(ApartmentState.STA);

            _elementProvider = elementProvider;
        }

        public async Task Start(TimeSpan timeSpan, Action<Application> onAppStarted = null)
        {
            if (_thread.ThreadState != System.Threading.ThreadState.Unstarted)
                throw new InvalidOperationException($"Cannot start when thread is in {_thread.ThreadState}");

            _thread.Start();
            await Task.Run(async () =>
            {
                if (!_manualResetEventSlim.Wait(timeSpan))
                {
                    throw new TimeoutException($"Did not start in the given time interval");
                }

                onAppStarted?.Invoke(_app);

                var loadawaiter = new ManualResetEventSlim();

                await _dispatcher.BeginInvoke(new Action(async () =>
                {
                    _window = new Window();

                    _element = _elementProvider(_app, _window);
                    _window.Content = _element;

                    _window.Show();

                    await _element.AwaitLoadedAsync();

                }));
            });
        }

        private void OnThreadStart()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            _dispatcher = Dispatcher.CurrentDispatcher;

            _app = new Application();

            _manualResetEventSlim.Set();

            _app.Run();
        }

        private System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            return null;
        }

        public async Task ExecuteWithApp(Action<Application> appAction)
        {
            await _dispatcher.BeginInvoke(new Action(() =>
            {
                appAction(_app);
            }));
        }

        public async Task Execute(Action<T> execute)
        {
            await _dispatcher.BeginInvoke(new Action(() =>
            {
                execute(_element);
            }));
        }

        public async Task<TResult> Execute<TResult>(Func<T, TResult> execute)
        {
            TResult result = default(TResult);

            await _dispatcher.BeginInvoke(new Action(() =>
            {
                result = execute(_element);
            }));

            return result;
        }
    }

    public static class ControlExtensions
    {
        public static async Task AwaitLoadedAsync(this FrameworkElement element)
        {
            if (element.IsLoaded)
                return;

            var resetEvent = new ManualResetEventSlim();

            element.Loaded += (o, e) =>
            {
                resetEvent.Set();
            };

            await Task.Run( () =>
            {
                resetEvent.Wait();
            });
        }

        public static T FindChild<T>(this DependencyObject parent, string childName)
           where T : DependencyObject
        {
            // Confirm parent and childName are valid. 
            if (parent == null) return null;

            T foundChild = null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                // If the child is not of the request child type child
                T childType = child as T;
                if (childType == null)
                {
                    // recursively drill down the tree
                    foundChild = FindChild<T>(child, childName);

                    // If the child is found, break so we do not overwrite the found child. 
                    if (foundChild != null) break;
                }
                else if (!string.IsNullOrEmpty(childName))
                {
                    var frameworkElement = child as FrameworkElement;
                    // If the child's name is set for search
                    if (frameworkElement != null && frameworkElement.Name == childName)
                    {
                        // if the child's name is of the request name
                        foundChild = (T)child;
                        break;
                    }
                }
                else
                {
                    // child element found.
                    foundChild = (T)child;
                    break;
                }
            }

            return foundChild;
        }
    }


    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public async Task TestMethod1()
        {
            var gui = new GUIThread<TestUserControl>((app, window) =>
            {
                var userControl = new TestUserControl();

                return userControl;

            });

            await gui.Start(TimeSpan.FromSeconds(10));

            var isEnabled = await gui.Execute(x => x.FindChild<Button>("testButton").IsEnabled);

            Assert.IsTrue(isEnabled);

            await gui.Execute(x => 
            {
                var button = x.FindChild<Button>("testButton");
                Attached.SetVerifier(button, null);
            });

            isEnabled = await gui.Execute(x => x.FindChild<Button>("testButton").IsEnabled);


            Assert.IsFalse(isEnabled);

            await gui.Execute(x =>
            {
                var button = x.FindChild<Button>("testButton");
                Attached.SetVerifier(button, new Verifier());
            });

            isEnabled = await gui.Execute(x => x.FindChild<Button>("testButton").IsEnabled);

            Assert.IsTrue(isEnabled);

        }
    }
}
