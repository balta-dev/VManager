using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace VManager.Controls
{
    /// <summary>
    /// WrapPanel que anima suavemente el reposicionamiento de sus hijos
    /// usando TranslateTransform + Transitions. El espacio reservado en el
    /// layout es correcto, por lo que los controles debajo se ubican bien.
    /// </summary>
    public class AnimatedWrapPanel : Panel
    {
        public static readonly StyledProperty<double> ItemSpacingProperty =
            AvaloniaProperty.Register<AnimatedWrapPanel, double>(nameof(ItemSpacing), 12);

        public static readonly StyledProperty<double> LineSpacingProperty =
            AvaloniaProperty.Register<AnimatedWrapPanel, double>(nameof(LineSpacing), 10);

        public static readonly StyledProperty<TimeSpan> AnimationDurationProperty =
            AvaloniaProperty.Register<AnimatedWrapPanel, TimeSpan>(nameof(AnimationDuration), TimeSpan.FromMilliseconds(250));

        public double ItemSpacing
        {
            get => GetValue(ItemSpacingProperty);
            set => SetValue(ItemSpacingProperty, value);
        }

        public double LineSpacing
        {
            get => GetValue(LineSpacingProperty);
            set => SetValue(LineSpacingProperty, value);
        }

        public TimeSpan AnimationDuration
        {
            get => GetValue(AnimationDurationProperty);
            set => SetValue(AnimationDurationProperty, value);
        }

        static AnimatedWrapPanel()
        {
            AffectsMeasure<AnimatedWrapPanel>(ItemSpacingProperty, LineSpacingProperty);
        }

        private bool _firstArrange = true;

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (var child in Children)
                child.Measure(availableSize);

            return CalculateLayout(availableSize, positions: null);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var positions = new Dictionary<int, Point>();
            CalculateLayout(finalSize, positions);

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];

                // Arrange siempre en (0,0) — el TranslateTransform hace el movimiento real
                child.Arrange(new Rect(new Point(0, 0), child.DesiredSize));

                if (!positions.TryGetValue(i, out var target))
                    continue;

                EnsureTranslateTransform(child, enableTransitions: !_firstArrange);

                var tt = (TranslateTransform)child.RenderTransform!;
                tt.X = target.X;
                tt.Y = target.Y;
            }

            _firstArrange = false;
            return finalSize;
        }

        /// <summary>
        /// Calcula las posiciones lógicas de los hijos.
        /// Si <paramref name="positions"/> es null, solo retorna el tamaño (para Measure).
        /// </summary>
        private Size CalculateLayout(Size constraint, Dictionary<int, Point>? positions)
        {
            double itemSpacing = ItemSpacing;
            double lineSpacing = LineSpacing;
            double maxWidth = constraint.Width;

            var lines = new List<List<(int index, Size size)>>();
            var currentLine = new List<(int index, Size size)>();
            double currentLineWidth = 0;

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                if (!child.IsVisible) continue;

                var size = child.DesiredSize;
                double neededWidth = currentLine.Count == 0
                    ? size.Width
                    : currentLineWidth + itemSpacing + size.Width;

                if (currentLine.Count > 0 && neededWidth > maxWidth)
                {
                    lines.Add(currentLine);
                    currentLine = new List<(int, Size)>();
                    currentLineWidth = 0;
                }

                currentLine.Add((i, size));
                currentLineWidth = currentLine.Count == 1
                    ? size.Width
                    : currentLineWidth + itemSpacing + size.Width;
            }

            if (currentLine.Count > 0)
                lines.Add(currentLine);

            double y = 0;
            double totalWidth = 0;

            foreach (var line in lines)
            {
                double lineWidth = 0;
                double lineHeight = 0;

                foreach (var (_, size) in line)
                {
                    lineWidth += size.Width;
                    lineHeight = Math.Max(lineHeight, size.Height);
                }
                lineWidth += itemSpacing * (line.Count - 1);

                double startX = Math.Max(0, (constraint.Width - lineWidth) / 2);
                double x = startX;

                foreach (var (idx, size) in line)
                {
                    positions?.Add(idx, new Point(x, y));
                    x += size.Width + itemSpacing;
                }

                totalWidth = Math.Max(totalWidth, lineWidth);
                y += lineHeight + lineSpacing;
            }

            if (lines.Count > 0)
                y -= lineSpacing;

            // Reportar el ancho completo disponible para que el padre
            // pueda centrar correctamente los controles que vengan debajo
            double reportedWidth = double.IsInfinity(constraint.Width) ? totalWidth : constraint.Width;
            return new Size(reportedWidth, Math.Max(y, 0));
        }

        private void EnsureTranslateTransform(Control child, bool enableTransitions)
        {
            if (child.RenderTransform is not TranslateTransform)
                child.RenderTransform = new TranslateTransform();

            var tt = (TranslateTransform)child.RenderTransform;

            if (!enableTransitions)
            {
                tt.Transitions = null;
                return;
            }

            // Solo reconstruir transitions si la duración cambió o no existen
            var duration = AnimationDuration;
            if (tt.Transitions is { Count: 2 }
                && tt.Transitions[0] is DoubleTransition dtX
                && dtX.Duration == duration)
                return;

            tt.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = TranslateTransform.XProperty,
                    Duration = duration,
                    Easing = new CubicEaseOut()
                },
                new DoubleTransition
                {
                    Property = TranslateTransform.YProperty,
                    Duration = duration,
                    Easing = new CubicEaseOut()
                }
            };
        }
    }
}
