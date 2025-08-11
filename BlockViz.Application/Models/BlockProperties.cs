using System.Windows;
using System.Windows.Media.Media3D;
using BlockViz.Domain.Models;

namespace BlockViz.Applications.Models
{
    public static class BlockProperties
    {
        private static readonly DependencyProperty BlockDataProperty =
            DependencyProperty.RegisterAttached(
                "BlockData", typeof(Block), typeof(BlockProperties), new PropertyMetadata(null));

        public static void SetData(ModelVisual3D element, Block value)
        {
            element.SetValue(BlockDataProperty, value);
        }

        public static Block GetData(ModelVisual3D element)
        {
            return (Block)element.GetValue(BlockDataProperty);
        }
    }
}