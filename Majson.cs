﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MajdataEdit
{
    class Majson
    {
        public string level = "1";
        public string difficulty = "EZ";
        public int diffNum = 0;
        public string title = "default";
        public string artist = "default";
        public string designer = "default";
        public List<SimaiTimingPoint> timingList = new List<SimaiTimingPoint>();
    }

    class EditRequestjson
    {
        public EditorControlMethod control;
        public float startTime;
        public long startAt;
        public string jsonPath;
        public float playSpeed;
        public float backgroundCover;
    }

    enum EditorControlMethod
    {
        Start,Stop
    }

    class MajSetting
    {
        public float playSpeed;
        public float backgroundCover;

        public int lastEditDiff;
        public double lastEditTime;

        public float BGM_Level;
        public float Tap_Level;
        public float Break_Level;
        public float Ex_Level;
        public float Hanabi_Level;
    }
}
