﻿using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ScanAGator.Analysis;
using ScanAGator.Imaging;

namespace ScanAGator.Controls
{
    public partial class AnalysisSettingsControl : UserControl
    {
        public Action<Analysis.AnalysisSettings>? Recalculate;
        public Timer RecalculateTimer = new() { Interval = 20, Enabled = true };
        private bool NeedsRecalculation;

        private Imaging.RatiometricImages? Images;
        private Bitmap? DisplayBitmap;
        private Prairie.ParirieXmlFile? PVXml;

        StructureRange StructureLast;
        public EventHandler<StructureRange> StructureChanged = delegate { };

        public AnalysisSettingsControl()
        {
            InitializeComponent();
            this.SizeChanged += (s, e) => OnLinescanImageChanged();
            cbDisplay.SelectedIndex = 0;

            cbDisplay.SelectedIndexChanged += CbDisplay_SelectedIndexChanged;
            tbFrame.ValueChanged += TbFrame_ValueChanged;
            cbAverage.CheckedChanged += CbAverage_CheckedChanged;
            cbFloor.CheckedChanged += CbFloor_CheckedChanged;
            nudFilterPx.ValueChanged += NudFilterPx_ValueChanged;
            cbFilter.CheckedChanged += CbFilter_CheckedChanged;

            tbBaseline1.ValueChanged += TrackBar_ValueChanged;
            tbBaseline2.ValueChanged += TrackBar_ValueChanged;
            tbStructure1.ValueChanged += TrackBar_ValueChanged;
            tbStructure2.ValueChanged += TrackBar_ValueChanged;

            nudBaseline1.ValueChanged += Nud_ValueChanged;
            nudBaseline2.ValueChanged += Nud_ValueChanged;
            nudStructure1.ValueChanged += Nud_ValueChanged;
            nudStructure2.ValueChanged += Nud_ValueChanged;

            RecalculateTimer.Tick += (e, a) => RecalculateIfNeeded();

            // enable double buffering (reflection required to mutate this private field)
            BindingFlags flags = BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(Panel).InvokeMember("DoubleBuffered", flags, null, panel1, new object[] { true });
            panel1.Paint += Panel1_Paint;
        }

        private string? CurrentFolderpath = string.Empty;

        public void SetLinescanFolder(string? folderPath)
        {
            CurrentFolderpath = folderPath;
            if (folderPath is null)
                return;

            Prairie.FolderContents pvFolder = new(folderPath);
            Prairie.ParirieXmlFile xml = new(pvFolder.XmlFilePath);
            Imaging.RatiometricImages images = new(pvFolder);
            if (cbFloor.Checked)
                images.SubtractFloorFromAllImages(20);

            SetMaxValues();

            PVXml = xml;
            Images = images;

            tbFrame.Value = 0;
            tbFrame.Maximum = Images.FrameCount - 1;
            lblLineScanTime.Text = xml.AcquisitionDate.ToString() + Environment.NewLine +
                $"X={xml.Position.X}, Y={xml.Position.Y}, Z={xml.Position.Z}";

            OnLinescanImageChanged();
            SetMaxValues();
            AutoBaseline();
            AutoStructure();
        }

        private void CbFloor_CheckedChanged(object sender, EventArgs e) => SetLinescanFolder(CurrentFolderpath);
        private void CbDisplay_SelectedIndexChanged(object sender, EventArgs e) => OnLinescanImageChanged();
        private void CbAverage_CheckedChanged(object sender, EventArgs e) => OnLinescanImageChanged();
        private void TbFrame_ValueChanged(object sender, EventArgs e) => OnLinescanImageChanged();
        private void TrackBar_ValueChanged(object sender, EventArgs e) => OnTrackbarChanged();
        private void NudFilterPx_ValueChanged(object sender, EventArgs e) => RecalculateSoon();
        private void CbFilter_CheckedChanged(object sender, EventArgs e) => RecalculateSoon();
        private void btnAutoBaseline_Click(object sender, EventArgs e) => AutoBaseline();
        private void btnAutoStructure_Click(object sender, EventArgs e) => AutoStructure();

        private void AutoBaseline(double b1Frac = .02, double b2Frac = .08)
        {
            Imaging.RatiometricImage? img = GetRatiometricImage();
            if (img is null)
                return;

            tbBaseline1.Value = tbBaseline1.Maximum - (int)(img.Green.Height * b1Frac);
            tbBaseline2.Value = tbBaseline2.Maximum - (int)(img.Green.Height * b2Frac);
            OnTrackbarChanged();
        }

        private void AutoStructure()
        {
            Imaging.RatiometricImage? img = GetRatiometricImage();
            if (img is null)
                return;

            StructureRange structure = StructureDetection.GetBrightestStructure(img.GreenData);
            tbStructure1.Value = structure.Min;
            tbStructure2.Value = structure.Max;
            OnTrackbarChanged();
        }

        public Imaging.RatiometricImage? GetRatiometricImage()
        {
            if (Images is null)
                return null;

            return cbAverage.Checked ? Images.Average : Images.Frames[tbFrame.Value];
        }

        private void OnLinescanImageChanged()
        {
            Imaging.RatiometricImage? img = GetRatiometricImage();

            if (img is null)
                return;

            DisplayBitmap = cbDisplay.Text switch
            {
                "Merge" => img.Merge,
                "Green" => img.Green,
                "Red" => img.Red,
                _ => throw new NotImplementedException(),
            };

            panel1.Invalidate();

            if (cbAverage.Checked)
            {
                lblFrame.Visible = false;
                tbFrame.Visible = false;
            }
            else
            {
                lblFrame.Text = $"Frame: {tbFrame.Value + 1}/{tbFrame.Maximum + 1}";
                lblFrame.Visible = true;
                tbFrame.Visible = true;
            }

            RecalculateSoon();
        }

        private void Nud_ValueChanged(object sender, EventArgs e)
        {
            OnNudChanged();
            panel1.Invalidate();
        }

        private void SetMaxValues()
        {
            int maxBaseline = DisplayBitmap is null ? 999999 : DisplayBitmap.Height - 1;
            int maxStructure = DisplayBitmap is null ? 999999 : DisplayBitmap.Width - 1;

            nudBaseline1.SetMax(maxBaseline);
            nudBaseline2.SetMax(maxBaseline);
            nudStructure1.SetMax(maxStructure);
            nudStructure2.SetMax(maxStructure);

            tbBaseline1.SetMax(maxBaseline);
            tbBaseline2.SetMax(maxBaseline);
            tbStructure1.SetMax(maxStructure);
            tbStructure2.SetMax(maxStructure);
        }


        private void OnTrackbarChanged()
        {
            nudBaseline1.SafeSet(tbBaseline1.Maximum - tbBaseline1.Value);
            nudBaseline2.SafeSet(tbBaseline2.Maximum - tbBaseline2.Value);
            nudStructure1.SafeSet(tbStructure1.Value);
            nudStructure2.SafeSet(tbStructure2.Value);
            RecalculateSoon();
        }

        private void OnNudChanged()
        {
            tbBaseline1.SafeSet(tbBaseline1.Maximum - (int)nudBaseline1.Value);
            tbBaseline2.SafeSet(tbBaseline2.Maximum - (int)nudBaseline2.Value);
            tbStructure1.SafeSet((int)nudStructure1.Value);
            tbStructure2.SafeSet((int)nudStructure2.Value);
            RecalculateSoon();
        }

        private void RecalculateSoon()
        {
            NeedsRecalculation = true;
        }

        private void RecalculateIfNeeded()
        {
            if (NeedsRecalculation)
                RecalculateNow();
        }

        public AnalysisSettings? RecalculateNow()
        {
            Imaging.RatiometricImage? ratioImage = GetRatiometricImage();

            if (ratioImage is null || PVXml is null || Images is null)
                return null;

            BaselineRange baseline = new((int)nudBaseline1.Value, (int)nudBaseline2.Value);
            StructureRange structure = new((int)nudStructure1.Value, (int)nudStructure2.Value, ratioImage.GreenData.Width);
            int filterPx = cbFilter.Checked ? (int)nudFilterPx.Value : 0;
            nudFilterPx.Enabled = cbFilter.Checked;
            double filterMs = filterPx * PVXml.MsecPerPixel;
            lblFilterTime.Text = $"{filterMs:N2} ms";

            AnalysisSettings settings = new(ratioImage, Images.Frames, baseline, structure, filterPx, ratioImage.FloorPercentile, PVXml);

            double[] redColumnData = ratioImage.RedData.AverageByColumn();
            double[] greenColumnData = ratioImage.GreenData.AverageByColumn();
            double ratioMinRed = redColumnData.Max() * .01;
            double[] ratioData = Enumerable
                .Range(0, redColumnData.Length)
                .Select(i => redColumnData[i] > ratioMinRed ? greenColumnData[i] / redColumnData[i] : 0)
                .ToArray();

            ScottPlot.Plot pltRaw = new(pbGraphRaw.Width, pbGraphRaw.Height);
            pltRaw.Frameless();
            pltRaw.AddSignal(greenColumnData, 1, Color.Green);
            pltRaw.AddSignal(redColumnData, 1, Color.Red);
            pltRaw.AddHorizontalSpan(structure.Min - .5, structure.Max + .5, Color.FromArgb(20, Color.Blue));
            pltRaw.AxisAutoX(0);
            pltRaw.Grid(false);
            pbGraphRaw.Image?.Dispose();
            pbGraphRaw.Image = pltRaw.GetBitmap();

            ScottPlot.Plot pltRatio = new(pbGraphRatio.Width, pbGraphRatio.Height);
            pltRatio.Frameless();
            pltRatio.AddSignal(ratioData, 1);
            pltRatio.AddHorizontalSpan(structure.Min - .5, structure.Max + .5, Color.FromArgb(20, Color.Blue));
            pltRatio.AxisAutoX(0);
            pltRatio.Grid(false);
            pbGraphRatio.Image?.Dispose();
            pbGraphRatio.Image = pltRatio.GetBitmap();

            Recalculate?.Invoke(settings);
            StructureChanged.Invoke(this, structure);
            NeedsRecalculation = false;

            return settings;
        }

        private void Panel1_Paint(object sender, PaintEventArgs e)
        {
            if (DisplayBitmap is null)
                return;

            Graphics gfx = e.Graphics;
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

            float pxPerPxY = (float)panel1.Height / DisplayBitmap.Height;
            BaselineRange baseline = new(DisplayBitmap.Height - tbBaseline1.Value, DisplayBitmap.Height - tbBaseline2.Value);
            float b1y = (baseline.Min - .5f) * pxPerPxY;
            float b2y = (baseline.Max + .5f) * pxPerPxY;

            float pxPerPxX = (float)panel1.Width / DisplayBitmap.Width;
            float s1x = (tbStructure1.Value - .5f) * pxPerPxX;
            float s2x = (tbStructure2.Value + .5f) * pxPerPxX;

            gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;

            gfx.DrawImage(DisplayBitmap, 0, 0, panel1.Width, panel1.Height);

            gfx.DrawLine(Pens.Yellow, 0, b1y, panel1.Width, b1y);
            gfx.DrawLine(Pens.Yellow, 0, b2y, panel1.Width, b2y);
            gfx.DrawLine(Pens.Yellow, s1x, 0, s1x, panel1.Height);
            gfx.DrawLine(Pens.Yellow, s2x, 0, s2x, panel1.Height);
        }
    }
}
