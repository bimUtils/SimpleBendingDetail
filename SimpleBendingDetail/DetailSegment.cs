namespace SimpleBendingDetail
{
    internal class DetailSegment
    {
        public double XOffset { get; set; }
        public double YOffset { get; set; }
        public double Length { get; set; }
        public double ArcRadius { get; set; }
        public double Rotation { get; set; }
        public double MinLabel { get; set; }
        public double MaxLabel { get; set; }
        public bool IsStartSegment { get; set; }
        public bool IsEndSegment { get; set; }

        // the family no longer needs this parameter. If Length == 0, then Visibility if false
        //public bool Visibility { get; set; } 

        public DetailSegment()
        {
            XOffset = 0;
            YOffset = 0;
            Length = 10;
            ArcRadius = 0;
            Rotation = 0;
            MinLabel = 0;
            MaxLabel = 0;
            IsStartSegment = false;
            IsEndSegment = false;
//            Visibility = false;
        }




    }
}
