using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace APDilution
{
    internal class PlateViewer : FrameworkElement
    {
        Size _size;
        int _col = 12;
        int _row = 8;
        Size _szMargin;
        Pen defaultPen;

        Point ptCurrentWell;
        Point ptSelectedWell = new Point(-1, -1);
        List<int> notUsedWellIDs = new List<int>();
        public delegate void selectedWellChangedHandler(string sNewWell);
        public event selectedWellChangedHandler OnSelectedWellChanged;
        Plate2Show plate2Show = Plate2Show.dilution;

        List<DilutionInfo> dilutionInfos = new List<DilutionInfo>();
        List<PipettingInfo> firstPlateBuffer = new List<PipettingInfo>();
        List<PipettingInfo> secondPlateBuffer = new List<PipettingInfo>();
        public PlateViewer(Size sz, Size szMargin, int col = 12, int row = 8)
        {
            _size = sz;
            _szMargin = szMargin;
            _col = col;
            _row = row;
            defaultPen = new Pen(Brushes.Black, 1);
            //this.MouseDown += new System.Windows.Input.MouseButtonEventHandler(MyFrameWorkElement_MouseDown);
            this.MouseUp += new System.Windows.Input.MouseButtonEventHandler(MyFrameWorkElement_MouseUp);
            this.MouseMove += new System.Windows.Input.MouseEventHandler(MyFrameWorkElement_MouseMove);
        }


        internal void SetNotUsed(List<int> wellIDs)
        {
            notUsedWellIDs = wellIDs;
            InvalidateVisual();
        }

        void MyFrameWorkElement_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Point ptTmp = e.GetPosition(this);
            ptCurrentWell = GetPos(ptTmp);
            //int colStart = (int)((ptTmp.X - _szMargin.Width) / GetWellWidth());
            //int rowStart = (int)((ptTmp.Y - _szMargin.Height) / GetWellHeight());
            //ptEnd = e.GetPosition(this);
            //sWellDesc = GetDescription(ptEnd);
            this.InvalidateVisual(); // cause a render
        }
        internal string GetDescription(Point curPt)
        {
            curPt.X += GetWellWidth() / 2;
            curPt.Y += GetWellHeight() / 2;
            Point pos = GetPos(curPt);
            if (pos.Y >= 8)
                return "";
            string sWellDesc = string.Format("{0}{1} ", (char)(pos.Y + 'A'), (pos.X + 1));
            return sWellDesc;
        }

        void MyFrameWorkElement_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //AdjustStartEndPosition();
            var pt = e.GetPosition(this);
            ptSelectedWell = GetPos(pt);

            this.InvalidateVisual();
        }

        void MyFrameWorkElement_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //ptStart = e.GetPosition(this);
        }

        private Pen GetDefaultPen()
        {
            return new Pen(Brushes.DarkGray, 1);
        }

        private Pen CreateBorderPen()
        {
            return new Pen(Brushes.Blue, 4);
        }

        private double GetWellWidth()
        {
            return (_size.Width - _szMargin.Width) / _col;
        }

        private double GetWellHeight()
        {
            return (_size.Height - _szMargin.Height) / _row;
        }

        public Point AdjustPosition(Point pt)
        {
            Point ptNew = new Point();
            int col = (int)((pt.X - _szMargin.Width) / GetWellWidth());
            ptNew.X = col * GetWellWidth() + _szMargin.Width;

            int row = (int)((pt.Y - _szMargin.Height) / GetWellHeight());
            ptNew.Y = (row + 1) * GetWellHeight() + _szMargin.Height;
            return ptNew;
        }

        public List<int> GetWellsInRegion(Point ptStart, Point ptEnd)
        {
            List<int> wells = new List<int>();
            int xStart = (int)ptStart.X;
            int yStart = (int)ptStart.Y;

            int xEnd = (int)ptEnd.X;
            int yEnd = (int)ptEnd.Y;

            for (int y = yStart; y < yEnd; y++)
            {
                for (int x = xStart; x < xEnd; x++)
                {
                    wells.Add(x * _row + y + 1);
                }
            }
            return wells;
        }

        private void DrawWells(DrawingContext drawingContext)
        {
            for (int i = 0; i < 96; i++)
            {
                DrawWell(i + 1, drawingContext);
            }
        }

        public static void Convert(int wellID, out int col, out int row)
        {
            int _row = 8;
            col = (wellID - 1) / _row;
            row = wellID - col * _row - 1;
        }

        private void DrawWell(int wellID, DrawingContext drawingContext)
        {
            int col, row;
            Convert(wellID, out col, out row);
            int xStart = (int)(col * GetWellWidth() + _szMargin.Width);
            int yStart = (int)(row * GetWellHeight() + _szMargin.Height);
            //bool hangOver = IsSamePosition(col, row, ptCurrentWell);
            Pen pen = defaultPen;
            bool selected = IsSamePosition(col, row, ptSelectedWell);
            if (selected)
            {
                pen = new Pen(Brushes.Blue, 1);
                if (OnSelectedWellChanged != null)
                    OnSelectedWellChanged(GetDescription(new Point(xStart, yStart)));
            }
      
            switch(plate2Show)
            {
                case Plate2Show.dilution:
                    DrawDilutionInfo(wellID,xStart,yStart,drawingContext);
                    break;
                case Plate2Show.reaction1:
                case Plate2Show.reaction2:
                    
                    DrawReaction(wellID,xStart,yStart,drawingContext);
                    break;
                default:
                    throw new Exception("Invalid plate, only dilution, reaction plate are supported!");
            }
            
            if (selected)
            {
                string sDesc = GetDescription(new Point(xStart, yStart));
                var txt = new FormattedText(
                 sDesc,
                 System.Globalization.CultureInfo.CurrentCulture,
                 FlowDirection.LeftToRight,
                 new Typeface("Courier new"),
                 24,
                 Brushes.Red);
                drawingContext.DrawText(txt, new Point(xStart + GetWellWidth() / 3, yStart + GetWellHeight() / 3));
            }

        }

        private void DrawReaction(int wellID, int xStart, int yStart, DrawingContext drawingContext)
        {
            double width = GetWellWidth();
            double height = GetWellHeight();
            PipettingInfo pipettingInfo = null;
            var plateBuffer = plate2Show == Plate2Show.reaction1 ? firstPlateBuffer : secondPlateBuffer;
            if (plateBuffer == null || !plateBuffer.Exists(x => x.dstWellID == wellID))
                return;
            DrawColor(wellID, xStart, yStart, drawingContext);
            pipettingInfo = plateBuffer.Where(x => x.dstWellID == wellID).First();
            bool firstBuffer = pipettingInfo.srcLabware == Configurations.Instance.Buffer1LabwareName;
            if(firstBuffer)
            {
                drawingContext.DrawEllipse(null, new Pen(Brushes.Red, 2), new Point(xStart + width / 2, yStart + height / 2), width / 8, height / 8);
            }
            int xOffset = 10;
            string txt = pipettingInfo.animalNo;
            string lowerTxt = pipettingInfo.dilutionTimes.ToString();
            DrawText(txt, new Point(xStart + xOffset / 3, yStart + height / 10), drawingContext, 16);
            DrawText(lowerTxt, new Point(xStart + xOffset, yStart + height / 3), drawingContext);
        }

        private void DrawColor(int wellID, int xStart, int yStart, DrawingContext drawingContext)
        {
            Pen pen = defaultPen;
            double height = GetWellHeight();
            Brush tmpBrush = GetBrush(wellID);
            drawingContext.DrawRectangle(tmpBrush, pen,
                    new Rect(new Point(xStart + 1, yStart + 1), new Size(GetWellWidth() - 1, height - 1)));
        }

        private void DrawDilutionInfo(int wellID, int xStart, int yStart, DrawingContext drawingContext)
        {
            if (!dilutionInfos.Exists(x => x.destWellID == wellID))
                return;
            DrawColor(wellID, xStart, yStart, drawingContext);
            var dilutionInfo = dilutionInfos.Where(x => x.destWellID == wellID).First();
            string sType = dilutionInfo.type == SampleType.Norm ? "" : dilutionInfo.type.ToString();
            string upperLine = string.Format("{0}{1:D2}", sType, dilutionInfo.seqIDinThisType);
            if (Configurations.Instance.IsGradualPipetting && dilutionInfo.type == SampleType.Norm)
            {
                upperLine = string.Format("{0}{1:D2}_{2}", sType, dilutionInfo.seqIDinThisType, dilutionInfo.gradualStep);
            }

            string lowerLine = GetDilutionDescription(dilutionInfo);
            int xOffset = dilutionInfo.type == SampleType.Norm ? (int)(GetWellWidth() / 3) : 10;
            DrawText(upperLine, new Point(xStart + xOffset / 3, yStart + GetWellHeight() / 10), drawingContext, 16);
            DrawText(lowerLine, new Point(xStart + xOffset, yStart + GetWellHeight() / 3), drawingContext);
            //if (dilutionInfo.type == SampleType.STD)
            //    DrawText("ng/mL", new Point(xStart + GetWellWidth() / 5, yStart + GetWellHeight() * 0.6), drawingContext);
        }

        private void DrawText(string str, Point point, DrawingContext drawingContext, int fontSize = 16)
        {
            if (str == null)
                return;

            var txt = new FormattedText(
               str,
               System.Globalization.CultureInfo.CurrentCulture,
               FlowDirection.LeftToRight,
               new Typeface("Courier new"),
               fontSize,
               Brushes.Black);

            drawingContext.DrawText(txt, point);
        }

        private string GetDilutionDescription(DilutionInfo dilutionInfo)
        {
            switch(dilutionInfo.type)
            {
                //case SampleType.Norm:
                //    return (ExcelReader.OrgSTDConc/dilutionInfo.dilutionTimes).ToString();
                case SampleType.STD:
                    //return (ExcelReader.OrgSTDConc / dilutionInfo.dilutionTimes).ToString();
                case SampleType.HQC:
                case SampleType.LQC:
                case SampleType.MQC:
                case SampleType.Norm:
                    return ((int)(dilutionInfo.dilutionTimes)).ToString();
                default:
                    return "";
            }
        }

        private Brush GetBrush(int wellID)
        {

            SampleType type = GetType(wellID);
            
            Dictionary<SampleType, Brush> type_Brush = new Dictionary<SampleType, Brush>();
            type_Brush.Add(SampleType.Norm, Brushes.Green);
            type_Brush.Add(SampleType.MatrixBlank, Brushes.White);
            type_Brush.Add(SampleType.HQC, Brushes.LightPink);
            type_Brush.Add(SampleType.MQC, Brushes.LightPink);
            type_Brush.Add(SampleType.LQC, Brushes.LightPink);
            type_Brush.Add(SampleType.STD,Brushes.LightBlue);
            type_Brush.Add(SampleType.Empty, Brushes.LightGray);
            return type_Brush[type];
        }

        private SampleType GetType(int wellID)
        {
            SampleType type = SampleType.Empty;
            switch(plate2Show)
            {
                case Plate2Show.dilution:
                    type = dilutionInfos.Where(x => x.destWellID == wellID).First().type;
                    break;
                case Plate2Show.reaction1:
                case Plate2Show.reaction2:
                    var plateBuffer = plate2Show == Plate2Show.reaction1 ? firstPlateBuffer : secondPlateBuffer;
                    if(plateBuffer.Exists(x => x.dstWellID == wellID))
                        type = plateBuffer.Where(x => x.dstWellID == wellID).First().type;
                    break;
                default:
                    throw new Exception("Not supported plate drawing.");
            }
            return type;
            
        }

        private bool IsSamePosition(int col, int row, Point ptWell)
        {
            return (col == ptWell.X && row == ptWell.Y);
        }



        private void DrawLabels(DrawingContext drawingContext)
        {
            for (int x = 1; x < _col + 1; x++)
            {
                var txt = new FormattedText(
                x.ToString(),
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Courier new"),
                20,
                Brushes.Black);

                int xPos = (int)((x - 0.6) * GetWellWidth()) + (int)_szMargin.Width;

                drawingContext.DrawText(txt,
                new Point(xPos, _szMargin.Height - 20)
                );
                //drawingContext.DrawLine(new Pen(defaultLineBrush, 1), new Point(xPos, 0), new Point(xPos, _size.Height));
            }


            for (int y = 1; y < _row + 1; y++)
            {
                var txt = new FormattedText(
                ((char)('A' + y - 1)).ToString(),
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Courier new"),
                20,
                Brushes.Black);

                int yPos = (int)((y - 0.7) * GetWellHeight()) + (int)_szMargin.Height;

                drawingContext.DrawText(txt,
                new Point(0, yPos)
                );

                //drawingContext.DrawLine(new Pen(defaultLineBrush, 1), new Point(xPos, 0), new Point(xPos, _size.Height));
            }
        }

        private void DrawGrids(DrawingContext drawingContext)
        {

            for (int x = 1; x < _col; x++)
            {
                int xPos = (int)(x * GetWellWidth()) + (int)_szMargin.Width;
                drawingContext.DrawLine(defaultPen, new Point(xPos, _szMargin.Height), new Point(xPos, _size.Height));
            }

            for (int y = 1; y < _row; y++)
            {
                int yPos = (int)(y * GetWellHeight()) + (int)_szMargin.Height;
                drawingContext.DrawLine(defaultPen, new Point(_szMargin.Width, yPos), new Point(_size.Width, yPos));
            }
        }


        private void DrawBorder(DrawingContext drawingContext)
        {
            drawingContext.DrawRectangle(Brushes.Transparent, defaultPen,
                new Rect(_szMargin.Width, _szMargin.Height, _size.Width - _szMargin.Width, _size.Height - _szMargin.Height));
        }



        private Point GetPos(Point pt)
        {
            int xStart = (int)((pt.X - _szMargin.Width) / GetWellWidth());
            int yStart = (int)((pt.Y - _szMargin.Height) / GetWellHeight());
            return new Point(xStart, yStart);
        }


        protected override void OnRender(DrawingContext drawingContext)
        {
            //draw border
            DrawBorder(drawingContext);
            //grids
            DrawGrids(drawingContext);
            //label
            DrawLabels(drawingContext);
            if (dilutionInfos == null || dilutionInfos.Count == 0)
                return;
            DrawWells(drawingContext);
        }

        private int GetWellID(int x, int y)
        {
            return x * _row + y + 1;
        }



        internal void SetSelectedWellID(int val)
        {
            int col, row;
            Convert(val, out col, out row);
            ptSelectedWell = new Point(col, row);
            InvalidateVisual();
        }

        internal void SetDilutionInfos(List<DilutionInfo> infos)
        {
            dilutionInfos = infos;
            InvalidateVisual();
        }

        internal void SetBuffer(List<PipettingInfo> firstPlateBuffer, List<PipettingInfo> secondPlateBuffer)
        {
            this.firstPlateBuffer = firstPlateBuffer;
            this.secondPlateBuffer = secondPlateBuffer;
        }

        internal void SetCurrentPlate(Plate2Show plate2Show)
        {
            this.plate2Show = plate2Show;
            InvalidateVisual();
        }
    }

    internal enum Plate2Show
    {
        dilution,
        reaction1,
        reaction2
    }
}
