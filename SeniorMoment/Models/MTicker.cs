using System;
using SeniorMoment.Services;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SeniorMoment.Models
{
    ///// <summary>
    ///// Ensure all MTimers are registered 
    ///// </summary>
    //public abstract class MTicker : IDisposable, ITick
    //{
    //    /// <summary>
    //    /// Abstract class MTicker has interfaces ITick. Anything that inherits from
    //    /// MTicker must define its own Tick() event. 
    //    /// </summary>
    //    public MTicker()
    //    {
    //        MTickTock.This.AddMTimer(this);
    //    }
    //    public abstract void Trace(string info,
    //                        string member = "",
    //                        int line = 0,
    //                        string path = "");
    //    public virtual void Dispose() => MTickTock.This.RemoveTicker(this);
    //    public abstract void Tick();
    //}
}
