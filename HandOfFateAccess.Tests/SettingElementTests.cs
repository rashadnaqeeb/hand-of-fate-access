using HandOfFateAccess.UI;
using Xunit;

namespace HandOfFateAccess.Tests {
	/// <summary>
	/// The settings-row readouts: sliders (value drawn as a fill sprite, worded as a
	/// percentage), label-valued rows (toggles/selectors), and key-binding rows. Each
	/// also exposes a value-only readout so an in-place left/right change re-speaks
	/// just the new value, not the whole row.
	/// </summary>
	public class SettingElementTests {

		[Fact]
		public void Slider_speaks_title_then_percent() {
			Assert.Equal("Music, 70%", new SliderElement(new[] { "Music" }, 0.7f).Describe().Resolve());
		}

		[Fact]
		public void Slider_value_only_is_percent() {
			Assert.Equal("70%", new SliderElement(new[] { "Music" }, 0.7f).DescribeValue().Resolve());
		}

		[Fact]
		public void Slider_rounds_to_nearest_whole_percent() {
			Assert.Equal("33%", new SliderElement(new string[0], 0.333f).DescribeValue().Resolve());
			// The 0.1 keyboard step accumulates float error (0.7000001); rounding hides it.
			Assert.Equal("70%", new SliderElement(new string[0], 0.7000001f).DescribeValue().Resolve());
		}

		[Fact]
		public void Slider_extremes_speak_zero_and_hundred() {
			Assert.Equal("0%", new SliderElement(new string[0], 0f).DescribeValue().Resolve());
			Assert.Equal("100%", new SliderElement(new string[0], 1f).DescribeValue().Resolve());
		}

		[Fact]
		public void Slider_with_no_title_speaks_percent_alone() {
			Assert.Equal("70%", new SliderElement(null, 0.7f).Describe().Resolve());
		}

		[Fact]
		public void Setting_speaks_labels_then_value() {
			Assert.Equal("Resolution, 1920 x 1080",
				new SettingElement(new[] { "Resolution" }, "1920 x 1080").Describe().Resolve());
		}

		[Fact]
		public void Setting_value_only_is_the_value() {
			Assert.Equal("On", new SettingElement(new[] { "Subtitles" }, "On").DescribeValue().Resolve());
		}

		[Fact]
		public void Setting_with_no_title_speaks_value_alone() {
			Assert.Equal("On", new SettingElement(null, "On").Describe().Resolve());
		}

		[Fact]
		public void Binding_speaks_action_then_key() {
			Assert.Equal("Move Up, W", new BindingElement("Move Up", "W", invalid: false).Describe().Resolve());
		}

		[Fact]
		public void Binding_value_only_is_the_key() {
			Assert.Equal("W", new BindingElement("Move Up", "W", invalid: false).DescribeValue().Resolve());
		}

		[Fact]
		public void Invalid_binding_appends_conflict() {
			Assert.Equal("Move Up, W, conflict", new BindingElement("Move Up", "W", invalid: true).Describe().Resolve());
			Assert.Equal("W, conflict", new BindingElement("Move Up", "W", invalid: true).DescribeValue().Resolve());
		}

		[Fact]
		public void Elements_without_a_separable_value_report_none() {
			Assert.Null(new GenericElement("Button", new[] { "Continue" }).DescribeValue());
		}
	}
}
