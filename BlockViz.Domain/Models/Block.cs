using System;
using CsvHelper.Configuration.Attributes;

namespace BlockViz.Domain.Models
{
    public class Block
    {
        [Name("BlockName")]
        public string Name { get; set; }

        [Name("StartDate")]
        public DateTime Start { get; set; }

        [Name("EndDate")]
        public DateTime End { get; set; }

        [Name("ProcessingTime")]
        public int ProcessingTime { get; set; }

        [Name("Length")]
        public double Length { get; set; }

        [Name("Breadth")]
        public double Breadth { get; set; }

        [Name("Depth")]
        public double Height { get; set; }

        [Name("DeployWorkplace")]
        public int DeployWorkplace { get; set; }

        [Name("NumberOfBlocks")]
        public int NumberOfBlocks { get; set; }

        [Name("BlockIDNumber")]
        public int BlockID { get; set; }

        [Name("BlockxCoord")]
        public int X { get; set; }

        [Name("BlockyCoord")]
        public int Y { get; set; }

        [Name("BlockDirection")]
        public int Direction { get; set; }
    }
}