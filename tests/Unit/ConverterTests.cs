using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using WileyWidget.Converters;
using Xunit;

namespace WileyWidget.Tests.Unit
{
    public class ConverterTests
    {
        #region BooleanToVisibilityConverter Tests

        [Fact]
        public void BooleanToVisibilityConverter_TrueValue_ReturnsVisible()
        {
            var converter = new BooleanToVisibilityConverter();
            var result = converter.Convert(true, typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Visible, result);
        }

        [Fact]
        public void BooleanToVisibilityConverter_FalseValue_ReturnsCollapsed()
        {
            var converter = new BooleanToVisibilityConverter();
            var result = converter.Convert(false, typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void BooleanToVisibilityConverter_NonBooleanValue_ReturnsCollapsed()
        {
            var converter = new BooleanToVisibilityConverter();
            var result = converter.Convert("not boolean", typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void BooleanToVisibilityConverter_ConvertBack_Visible_ReturnsTrue()
        {
            var converter = new BooleanToVisibilityConverter();
            var result = converter.ConvertBack(Visibility.Visible, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.True((bool)result);
        }

        [Fact]
        public void BooleanToVisibilityConverter_ConvertBack_Collapsed_ReturnsFalse()
        {
            var converter = new BooleanToVisibilityConverter();
            var result = converter.ConvertBack(Visibility.Collapsed, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.False((bool)result);
        }

        #endregion

        #region ComparisonConverter Tests

        [Fact]
        public void ComparisonConverter_ValueGreaterThanParameter_ReturnsOne()
        {
            var converter = new ComparisonConverter();
            var result = converter.Convert(10.0, typeof(int), "5", CultureInfo.InvariantCulture);
            Assert.Equal(1, result);
        }

        [Fact]
        public void ComparisonConverter_ValueLessThanParameter_ReturnsNegativeOne()
        {
            var converter = new ComparisonConverter();
            var result = converter.Convert(5.0, typeof(int), "10", CultureInfo.InvariantCulture);
            Assert.Equal(-1, result);
        }

        [Fact]
        public void ComparisonConverter_ValueEqualToParameter_ReturnsZero()
        {
            var converter = new ComparisonConverter();
            var result = converter.Convert(10.0, typeof(int), "10", CultureInfo.InvariantCulture);
            Assert.Equal(0, result);
        }

        [Fact]
        public void ComparisonConverter_NullValue_ReturnsZero()
        {
            var converter = new ComparisonConverter();
            var result = converter.Convert(null, typeof(int), "10", CultureInfo.InvariantCulture);
            Assert.Equal(0, result);
        }

        #endregion

        #region StatusToColorConverter Tests

        [Fact]
        public void StatusToColorConverter_ErrorStatus_ReturnsRed()
        {
            var converter = new StatusToColorConverter();
            var result = converter.Convert("Error occurred", typeof(Brush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.Red, result);
        }

        [Fact]
        public void StatusToColorConverter_WarningStatus_ReturnsOrange()
        {
            var converter = new StatusToColorConverter();
            var result = converter.Convert("Warning message", typeof(Brush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.Orange, result);
        }

        [Fact]
        public void StatusToColorConverter_SuccessStatus_ReturnsGreen()
        {
            var converter = new StatusToColorConverter();
            var result = converter.Convert("Success completed", typeof(Brush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.Green, result);
        }

        [Fact]
        public void StatusToColorConverter_NullValue_ReturnsBlack()
        {
            var converter = new StatusToColorConverter();
            var result = converter.Convert(null, typeof(Brush), null, CultureInfo.InvariantCulture);
            Assert.Equal(Brushes.Black, result);
        }

        #endregion

        #region BalanceColorConverter Tests

        [Fact]
        public void BalanceColorConverter_PositiveBalance_ReturnsGreenBrush()
        {
            var converter = new BalanceColorConverter();
            var result = converter.Convert(100.0m, typeof(Brush), null, CultureInfo.InvariantCulture);
            var brush = result as SolidColorBrush;
            Assert.NotNull(brush);
            Assert.Equal(Color.FromRgb(74, 222, 128), brush.Color);
        }

        [Fact]
        public void BalanceColorConverter_NegativeBalance_ReturnsRedBrush()
        {
            var converter = new BalanceColorConverter();
            var result = converter.Convert(-50.0m, typeof(Brush), null, CultureInfo.InvariantCulture);
            var brush = result as SolidColorBrush;
            Assert.NotNull(brush);
            Assert.Equal(Color.FromRgb(248, 113, 113), brush.Color);
        }

        [Fact]
        public void BalanceColorConverter_ZeroBalance_ReturnsNeutralBrush()
        {
            var converter = new BalanceColorConverter();
            var result = converter.Convert(0.0m, typeof(Brush), null, CultureInfo.InvariantCulture);
            var brush = result as SolidColorBrush;
            Assert.NotNull(brush);
            Assert.Equal(Color.FromRgb(185, 200, 236), brush.Color);
        }

        [Fact]
        public void BalanceColorConverter_PositiveVisibilityParameter_ReturnsVisible()
        {
            var converter = new BalanceColorConverter();
            var result = converter.Convert(100.0m, typeof(Visibility), "PositiveVisibility", CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Visible, result);
        }

        [Fact]
        public void BalanceColorConverter_NegativeVisibilityParameter_ReturnsVisible()
        {
            var converter = new BalanceColorConverter();
            var result = converter.Convert(-50.0m, typeof(Visibility), "NegativeVisibility", CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Visible, result);
        }

        #endregion

        #region ZeroToVisibleConverter Tests

        [Fact]
        public void ZeroToVisibleConverter_ZeroValue_ReturnsVisible()
        {
            var converter = new ZeroToVisibleConverter();
            var result = converter.Convert(0, typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Visible, result);
        }

        [Fact]
        public void ZeroToVisibleConverter_NonZeroValue_ReturnsCollapsed()
        {
            var converter = new ZeroToVisibleConverter();
            var result = converter.Convert(5, typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void ZeroToVisibleConverter_NonIntegerValue_ReturnsCollapsed()
        {
            var converter = new ZeroToVisibleConverter();
            var result = converter.Convert("not integer", typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        #endregion

        #region StringToVisibilityConverter Tests

        [Fact]
        public void StringToVisibilityConverter_NonEmptyString_ReturnsVisible()
        {
            var converter = new StringToVisibilityConverter();
            var result = converter.Convert("test", typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Visible, result);
        }

        [Fact]
        public void StringToVisibilityConverter_EmptyString_ReturnsCollapsed()
        {
            var converter = new StringToVisibilityConverter();
            var result = converter.Convert("", typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void StringToVisibilityConverter_InverseParameter_InvertsLogic()
        {
            var converter = new StringToVisibilityConverter();
            var result = converter.Convert("test", typeof(Visibility), "Inverse", CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        #endregion

        #region GreaterThanConverter Tests

        [Fact]
        public void GreaterThanConverter_ValueGreaterThanParameter_ReturnsVisible()
        {
            var converter = new GreaterThanConverter();
            var result = converter.Convert(10.0, typeof(Visibility), "5", CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Visible, result);
        }

        [Fact]
        public void GreaterThanConverter_ValueLessThanParameter_ReturnsCollapsed()
        {
            var converter = new GreaterThanConverter();
            var result = converter.Convert(3.0, typeof(Visibility), "5", CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void GreaterThanConverter_InvalidValue_ReturnsCollapsed()
        {
            var converter = new GreaterThanConverter();
            var result = converter.Convert("invalid", typeof(Visibility), "5", CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        #endregion

        #region NullToBoolConverter Tests

        [Fact]
        public void NullToBoolConverter_NonNullValue_ReturnsTrue()
        {
            var converter = new NullToBoolConverter();
            var result = converter.Convert("test", typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.True((bool)result);
        }

        [Fact]
        public void NullToBoolConverter_NullValue_ReturnsFalse()
        {
            var converter = new NullToBoolConverter();
            var result = converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);
            Assert.False((bool)result);
        }

        [Fact]
        public void NullToBoolConverter_InverseParameter_InvertsLogic()
        {
            var converter = new NullToBoolConverter();
            var result = converter.Convert("test", typeof(bool), "Inverse", CultureInfo.InvariantCulture);
            Assert.False((bool)result);
        }

        #endregion

        #region InverseBooleanToVisibilityConverter Tests

        [Fact]
        public void InverseBooleanToVisibilityConverter_TrueValue_ReturnsCollapsed()
        {
            var converter = new InverseBooleanToVisibilityConverter();
            var result = converter.Convert(true, typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void InverseBooleanToVisibilityConverter_FalseValue_ReturnsVisible()
        {
            var converter = new InverseBooleanToVisibilityConverter();
            var result = converter.Convert(false, typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Visible, result);
        }

        #endregion

        #region NullToVisibilityConverter Tests

        [Fact]
        public void NullToVisibilityConverter_NonNullValue_ReturnsVisible()
        {
            var converter = new NullToVisibilityConverter();
            var result = converter.Convert("test", typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Visible, result);
        }

        [Fact]
        public void NullToVisibilityConverter_NullValue_ReturnsCollapsed()
        {
            var converter = new NullToVisibilityConverter();
            var result = converter.Convert(null, typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void NullToVisibilityConverter_InverseParameter_InvertsLogic()
        {
            var converter = new NullToVisibilityConverter();
            var result = converter.Convert("test", typeof(Visibility), "Inverse", CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        #endregion

        #region EmptyStringToVisibilityConverter Tests

        [Fact]
        public void EmptyStringToVisibilityConverter_NonEmptyString_ReturnsVisible()
        {
            var converter = new EmptyStringToVisibilityConverter();
            var result = converter.Convert("test", typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Visible, result);
        }

        [Fact]
        public void EmptyStringToVisibilityConverter_EmptyString_ReturnsCollapsed()
        {
            var converter = new EmptyStringToVisibilityConverter();
            var result = converter.Convert("", typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        [Fact]
        public void EmptyStringToVisibilityConverter_NullString_ReturnsCollapsed()
        {
            var converter = new EmptyStringToVisibilityConverter();
            var result = converter.Convert(null, typeof(Visibility), null, CultureInfo.InvariantCulture);
            Assert.Equal(Visibility.Collapsed, result);
        }

        #endregion
    }
}