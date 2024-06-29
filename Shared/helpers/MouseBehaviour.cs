using System.Windows;
using System.Windows.Input;

namespace AlgoTrader.Shared
{
    /*
     * These are bindings to UIElement events, like UIElement.MouseUP
     * this is the syntax:
      
        //create the dependency property
        public static readonly DependencyProperty MouseWheelProperty = DependencyProperty.Register(
            "MouseWheel", typeof(int), typeof(MouseBehaviour), new PropertyMetadata(default(int)));

        //the bindable property which will be available in XAML for binding to the view model
        // - there will then be a property in the view model as well which this will be bound to
        public int MouseWheel
        {
            get { return (int)GetValue(MouseWheelProperty); }
            set { SetValue(MouseWheelProperty, value); }
        }

        //attach the MouseWheel event to a custom event handler
        //all events are available here 
        protected override void OnAttached()
        {
            AssociatedObject.MouseWheel += AssociatedObjectOnMouseWheel;
        }

        //handle the MouseWheel event, setting the dependency properties
        private void AssociatedObjectOnMouseWheel(object sender, MouseWheelEventArgs mouseWheelEventArgs)
        {
            MouseWheel = mouseWheelEventArgs.Delta;
        }

        //XAML:
        <i:Interaction.Behaviors>
           <!--bind custom mouse properties to the view model-->
           <mouseMoveMVVM:MouseBehaviour MouseWheel="{Binding MouseWheelViewModelProperty, Mode=OneWayToSource}" />
        </i:Interaction.Behaviors>

     * this will not bind a mouse down event to a button
     * additionally, the binding is for the entire object so it is not possible to do separate bindings to different objects in the same Behaviour class
     * */
    public class MouseBehaviour : System.Windows.Interactivity.Behavior<FrameworkElement>
    {
        
        //register the MouseDown property as a dependency property
        public static readonly DependencyProperty MouseDownProperty = DependencyProperty.Register(
            "MouseDown", typeof(Point), typeof(MouseBehaviour), new PropertyMetadata(default(Point)));

        //this property is now bindable to something in the view model
        public Point MouseDown
        {
            get { return (Point)GetValue(MouseDownProperty); }
            set { SetValue(MouseDownProperty, value); }
        }

        //register the MouseUp property as a dependency property
        public static readonly DependencyProperty MouseUpProperty = DependencyProperty.Register(
            "MouseUp", typeof(Point), typeof(MouseBehaviour), new PropertyMetadata(default(Point)));

        //this property is now bindable to something in the view model
        public Point MouseUp
        {
            get { return (Point)GetValue(MouseUpProperty); }
            set { SetValue(MouseUpProperty, value); }
        }

        //register the MouseRightButtonDown property as a dependency property
        public static readonly DependencyProperty MouseRightButtonDownProperty = DependencyProperty.Register(
            "MouseRightButtonDown", typeof(Point), typeof(MouseBehaviour), new PropertyMetadata(default(Point)));

        //this property is now bindable to something in the view model
        public Point MouseRightButtonDown
        {
            get { return (Point)GetValue(MouseRightButtonDownProperty); }
            set 
            { 
                SetValue(MouseRightButtonDownProperty, value); 
            }
        }
        /*
        //register the MouseUp property as a dependency property
        public static readonly DependencyProperty MouseRightButtonUpProperty = DependencyProperty.Register(
            "MouseRightButtonUp", typeof(Point), typeof(MouseBehaviour), new PropertyMetadata(default(Point)));

        //this property is now bindable to something in the view model
        public Point MouseRightButtonUp
        {
            get { return (Point)GetValue(MouseRightButtonUpProperty); }
            set { SetValue(MouseRightButtonUpProperty, value); }
        }

        //register the MouseUp property as a dependency property
        public static readonly DependencyProperty MouseLeftButtonUpProperty = DependencyProperty.Register(
            "MouseLeftButtonUp", typeof(Point), typeof(MouseBehaviour), new PropertyMetadata(default(Point)));

        //this property is now bindable to something in the view model
        public Point MouseLeftButtonUp
        {
            get { return (Point)GetValue(MouseLeftButtonUpProperty); }
            set { SetValue(MouseLeftButtonUpProperty, value); }
        }
        */

        //register the MouseY property as a dependency property
        public static readonly DependencyProperty MouseYProperty = DependencyProperty.Register(
            "MouseY", typeof(double), typeof(MouseBehaviour), new PropertyMetadata(default(double)));

        //this property is now bindable to something in the view model
        public double MouseY
        {
            get { return (double)GetValue(MouseYProperty); }
            set { SetValue(MouseYProperty, value); }
        }

        public static readonly DependencyProperty MouseXProperty = DependencyProperty.Register(
            "MouseX", typeof(double), typeof(MouseBehaviour), new PropertyMetadata(default(double)));

        public double MouseX
        {
            get { return (double)GetValue(MouseXProperty); }
            set { SetValue(MouseXProperty, value); }
        }

        public static readonly DependencyProperty MouseWheelProperty = DependencyProperty.Register(
            "MouseWheel", typeof(int), typeof(MouseBehaviour), new PropertyMetadata(default(int)));

        public int MouseWheel
        {
            get { return (int)GetValue(MouseWheelProperty); }
            set { SetValue(MouseWheelProperty, value); }
        }

        //attach the MouseMove event to a custom event handler
        //all events are available here 
        protected override void OnAttached()
        {
            AssociatedObject.MouseMove += AssociatedObjectOnMouseMove;
            AssociatedObject.MouseDown += AssociatedObjectOnMouseDown;
            AssociatedObject.MouseUp += AssociatedObjectOnMouseUp;
            AssociatedObject.MouseRightButtonDown += AssociatedObjectOnMouseRightButtonDown;
           // AssociatedObject.MouseLeftButtonUp += AssociatedObjectOnMouseLeftButtonUp;
            AssociatedObject.MouseWheel += AssociatedObjectOnMouseWheel;
        }

        //handle the MouseMove event, setting the dependency properties which can be bound to the ViewModel in XAML
        private void AssociatedObjectOnMouseMove(object sender, MouseEventArgs mouseEventArgs)
        {
            var pos = mouseEventArgs.GetPosition(AssociatedObject);
            MouseX = pos.X;
            MouseY = pos.Y;
        }

        //handle the MouseDown event, setting the dependency properties which can be bound to the ViewModel in XAML
        private void AssociatedObjectOnMouseDown(object sender, MouseEventArgs mouseEventArgs)
        {
            MouseDown = mouseEventArgs.GetPosition(AssociatedObject);
        }
        private void AssociatedObjectOnMouseUp(object sender, MouseEventArgs mouseEventArgs)
        {
            MouseUp = mouseEventArgs.GetPosition(AssociatedObject);
        }
       private void AssociatedObjectOnMouseRightButtonDown(object sender, MouseEventArgs mouseEventArgs)
        {
            MouseRightButtonDown = mouseEventArgs.GetPosition(AssociatedObject);
        }
        //private void AssociatedObjectOnMouseLeftButtonUp(object sender, MouseEventArgs mouseEventArgs)
        //{
        //    MouseLeftButtonUp = mouseEventArgs.GetPosition(AssociatedObject);
        //}
  

        private void AssociatedObjectOnMouseWheel(object sender, MouseWheelEventArgs mouseWheelEventArgs)
        {
            MouseWheel = mouseWheelEventArgs.Delta;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.MouseMove -= AssociatedObjectOnMouseMove;
            AssociatedObject.MouseDown -= AssociatedObjectOnMouseDown;
            AssociatedObject.MouseUp -= AssociatedObjectOnMouseUp;
            AssociatedObject.MouseWheel -= AssociatedObjectOnMouseWheel;
            //AssociatedObject.MouseRightButtonUp -= AssociatedObjectOnMouseRightButtonUp;
            //AssociatedObject.MouseLeftButtonUp -= AssociatedObjectOnMouseLeftButtonUp;
        }

    }

}
