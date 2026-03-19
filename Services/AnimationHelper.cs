using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EAClient.Services
{
    public static class AnimationHelper
    {
        public static void FadeIn(UIElement element, double durationMs = 300)
        {
            if (!AppTheme.Animations) return;
            element.Opacity = 0;
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs));
            element.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        public static void FadeInFromBelow(FrameworkElement element, double durationMs = 350)
        {
            if (!AppTheme.Animations) return;
            element.Opacity = 0;

            var transform = new TranslateTransform(0, 20);
            element.RenderTransform = transform;

            var fadeAnim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(durationMs));
            element.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            var slideAnim = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(durationMs))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            transform.BeginAnimation(TranslateTransform.YProperty, slideAnim);
        }
    }
}
