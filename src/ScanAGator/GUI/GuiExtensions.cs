﻿using System;
using System.Windows.Forms;

namespace ScanAGator.GUI;

public static class GuiExtensions
{
    public static void SetMax(this NumericUpDown nud, int newMax)
    {
        if (newMax < nud.Maximum)
        {
            if (nud.Value > newMax)
            {
                nud.Value = newMax;
            }
        }

        nud.Maximum = newMax;
    }

    public static void SetMax(this TrackBar tb, int newMax)
    {
        if (newMax < tb.Maximum)
        {
            if (tb.Value > newMax)
            {
                tb.Value = newMax;
            }
        }

        tb.Maximum = newMax;
    }

    public static void SafeSet(this NumericUpDown nud, int value)
    {
        nud.Value = Math.Min(nud.Maximum, value);
    }

    public static void SafeSet(this TrackBar tb, int value)
    {
        tb.Value = Math.Min(tb.Maximum, value);
    }
}