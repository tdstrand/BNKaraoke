using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;
using BNKaraoke.DJ.Models;
using CommunityToolkit.Mvvm.Input;

namespace BNKaraoke.DJ.Behaviors
{
    public class DragDropBehavior : Behavior<ListView>
    {
        public static readonly DependencyProperty DragCommandProperty =
            DependencyProperty.Register("DragCommand", typeof(IRelayCommand), typeof(DragDropBehavior));

        public static readonly DependencyProperty DropCommandProperty =
            DependencyProperty.Register("DropCommand", typeof(IRelayCommand), typeof(DragDropBehavior));

        public IRelayCommand DragCommand
        {
            get => (IRelayCommand)GetValue(DragCommandProperty);
            set => SetValue(DragCommandProperty, value);
        }

        public IRelayCommand DropCommand
        {
            get => (IRelayCommand)GetValue(DropCommandProperty);
            set => SetValue(DropCommandProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
            AssociatedObject.DragOver += OnDragOver;
            AssociatedObject.Drop += OnDrop;
            AssociatedObject.AllowDrop = true;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
            AssociatedObject.DragOver -= OnDragOver;
            AssociatedObject.Drop -= OnDrop;
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && AssociatedObject.SelectedItem != null)
            {
                var draggedItem = AssociatedObject.SelectedItem as QueueEntry;
                if (draggedItem != null && DragCommand?.CanExecute(draggedItem) == true)
                {
                    DragCommand.Execute(draggedItem);
                    var data = new DataObject(typeof(QueueEntry), draggedItem);
                    DragDrop.DoDragDrop(AssociatedObject, data, DragDropEffects.Move);
                }
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(QueueEntry)))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(QueueEntry)))
            {
                var droppedItem = e.Data.GetData(typeof(QueueEntry)) as QueueEntry;
                if (droppedItem != null && DropCommand?.CanExecute(droppedItem) == true)
                {
                    DropCommand.Execute(droppedItem);
                }
            }
            e.Handled = true;
        }
    }
}