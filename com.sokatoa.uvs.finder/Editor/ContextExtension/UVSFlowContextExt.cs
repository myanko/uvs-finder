using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.VisualScripting.UVSFinder
{
    [GraphContextExtension(typeof(FlowGraphContext))]
    public class UVSFlowContextExt : GraphContextExtension<FlowGraphContext>
    {
        public UVSFlowContextExt(FlowGraphContext context) : base(context) { }

        public override IEnumerable<GraphContextMenuItem> contextMenuItems { get {
                return GetContextOptions();
            } 
        }

        public void OnFind(Vector2 vector)
        {
            UVSFinder.ShowUVSFinder();
            UVSFinder window = (UVSFinder)UVSFinder.GetWindow(typeof(UVSFinder));
            window.PerformSearchInCurrent("");
        }

        public void OnFindAndReplace(Vector2 vector)
        {
            UVSFinder.ShowUVSFinder();
        }

        protected virtual IEnumerable<GraphContextMenuItem> GetContextOptions()
        {
            yield return new GraphContextMenuItem((Action<Vector2>)OnFind, "Find all in current graph");
           /* if (canvas.selection.Count > 0)
            {
                yield return new GraphContextMenuItem((Action<Vector2>)OnFindAndReplace, "Find and replace");
            }*/
        }
    }
}
