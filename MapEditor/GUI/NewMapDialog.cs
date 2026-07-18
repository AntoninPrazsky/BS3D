using System.Drawing;
using System.Windows.Forms;

namespace MapEditor.GUI
{
    /// <summary>
    /// Modal dialog for choosing the play field size of a new map.
    /// </summary>
    internal class NewMapDialog : Form
    {
        private readonly NumericUpDown _sizeX;
        private readonly NumericUpDown _sizeZ;
        private readonly NumericUpDown _levels;

        public byte StageSizeX => (byte)_sizeX.Value;
        public byte StageSizeZ => (byte)_sizeZ.Value;
        public byte Levels => (byte)_levels.Value;

        private const byte MINIMUM = 2; //BallsMap requires at least 2×2×2
        private const byte MAXIMUM = 32;

        public NewMapDialog(byte sizeX, byte sizeZ, byte levels)
        {
            Text = "New map";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(230, 140);

            _sizeX = AddRow("Stage size X:", sizeX, 0);
            _sizeZ = AddRow("Stage size Z:", sizeZ, 1);
            _levels = AddRow("Levels:", levels, 2);

            Button create = new()
            {
                Text = "Create",
                DialogResult = DialogResult.OK,
                Location = new Point(45, 105),
                Size = new Size(80, 25)
            };

            Button cancel = new()
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(135, 105),
                Size = new Size(80, 25)
            };

            Controls.Add(create);
            Controls.Add(cancel);

            AcceptButton = create;
            CancelButton = cancel;
        }

        private NumericUpDown AddRow(string labelText, byte value, int row)
        {
            int y = 12 + row * 30;

            Controls.Add(new Label()
            {
                Text = labelText,
                Location = new Point(12, y + 2),
                Size = new Size(90, 20)
            });

            NumericUpDown numeric = new()
            {
                Minimum = MINIMUM,
                Maximum = MAXIMUM,
                Value = System.Math.Clamp(value, MINIMUM, MAXIMUM),
                Location = new Point(110, y),
                Size = new Size(105, 25)
            };

            Controls.Add(numeric);
            return numeric;
        }
    }
}
