namespace PitmastersGrill.Services
{
    public sealed record PilotDetailWindowPlacement(
        double Left,
        double Top,
        string PreferredSide,
        string FinalSide,
        bool WasAdjusted);

    public sealed class PilotDetailWindowPlacementController
    {
        public PilotDetailWindowPlacement BuildPlacement(
            double detailWidth,
            double detailHeight,
            double ownerLeft,
            double ownerTop,
            double ownerWidth,
            double workLeft,
            double workTop,
            double workRight,
            double workBottom,
            bool preferLeft,
            double detailWindowGap)
        {
            var rightX = ownerLeft + ownerWidth + detailWindowGap;
            var leftX = ownerLeft - detailWidth - detailWindowGap;
            var canRight = rightX + detailWidth <= workRight;
            var canLeft = leftX >= workLeft;

            var preferredSide = preferLeft ? "left" : "right";
            var finalSide = preferredSide;

            if (preferLeft)
            {
                if (!canLeft && canRight)
                {
                    finalSide = "right";
                }
            }
            else if (!canRight && canLeft)
            {
                finalSide = "left";
            }

            var targetLeft = finalSide == "left" ? leftX : rightX;
            var targetTop = ownerTop;
            var clampedLeft = Clamp(targetLeft, workLeft, System.Math.Max(workLeft, workRight - detailWidth));
            var clampedTop = Clamp(targetTop, workTop, System.Math.Max(workTop, workBottom - detailHeight));
            var wasAdjusted = !AreClose(clampedLeft, targetLeft) || !AreClose(clampedTop, targetTop) || finalSide != preferredSide;

            return new PilotDetailWindowPlacement(
                clampedLeft,
                clampedTop,
                preferredSide,
                finalSide,
                wasAdjusted);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }

        private static bool AreClose(double left, double right)
        {
            return System.Math.Abs(left - right) < 0.5;
        }
    }
}
