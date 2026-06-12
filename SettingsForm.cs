namespace WindowsAudioSettingsEnforcer;

public sealed class SettingsForm : Form
{
    public SettingsForm(AudioOptions current, SettingsWriter writer)
    {
        Text = "Audio Settings Enforcer";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(320, 150);

        var volumeLabel = new Label
        {
            Text = "Volume (%)",
            Location = new Point(12, 12),
            AutoSize = true,
        };

        var volumeSlider = new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Value = Math.Clamp(current.VolumePercent, 0, 100),
            Location = new Point(12, 34),
            Size = new Size(220, 45),
        };

        var volumeBox = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp(current.VolumePercent, 0, 100),
            Location = new Point(244, 38),
            Size = new Size(64, 23),
        };

        volumeSlider.ValueChanged += (_, _) => volumeBox.Value = volumeSlider.Value;
        volumeBox.ValueChanged += (_, _) => volumeSlider.Value = (int)volumeBox.Value;

        var muteCheck = new CheckBox
        {
            Text = "Mute",
            Checked = current.Mute,
            Location = new Point(12, 82),
            AutoSize = true,
        };

        var saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(152, 114),
            Size = new Size(75, 26),
        };
        saveButton.Click += (_, _) =>
        {
            writer.Save(volumeSlider.Value, muteCheck.Checked);
            Close();
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(233, 114),
            Size = new Size(75, 26),
        };
        cancelButton.Click += (_, _) => Close();

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        Controls.AddRange(new Control[] { volumeLabel, volumeSlider, volumeBox, muteCheck, saveButton, cancelButton });
    }
}
