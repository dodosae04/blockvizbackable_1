using System;
using System.Collections.Generic;
using System.Windows.Media.Media3D;
using BlockViz.Domain.Models;  

namespace BlockViz.Applications.Services
{
    public interface IBlockArrangementService
    {
        IEnumerable<ModelVisual3D> Arrange(IEnumerable<Block> blocks, DateTime currentDate);
    }
}