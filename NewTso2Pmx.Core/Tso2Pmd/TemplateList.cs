using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using TDCG;
using TDCGUtils;

namespace Tso2Pmd
{
    /// <summary>
    /// 体型＆物理スクリプトのリストを扱います。
    /// </summary>
    public class TemplateList
    {
        /// <summary>
        /// 物理オブジェクトスクリプトのリスト
        /// </summary>
        public List<IPhysObTemplate> phys_items
        {
            get { return PhysObTemplateList.Instance.items; }
        }

        public Dictionary<IPhysObTemplate, bool> phys_flags
        {
            get { return PhysObTemplateList.Instance.flags; }
        }

        /// <summary>
        /// 体型＆物理スクリプトを読み込みます。
        /// </summary>
        public bool Load()
        {
            ProportionList.Instance.Load();
            PhysObTemplateList.Instance.Load();
            return true;
        }


        public void PhysObExecute(ref T2PPhysObjectList physOb_list)
        {
            foreach (IPhysObTemplate i in phys_items)
            {
                if (phys_flags[i] == true)
                {
                    try
                    {
                        i.Execute(ref physOb_list);
                    }
                    catch (KeyNotFoundException ex)
                    {
                        Console.WriteLine("Physics template skipped: " + i.Name() + " (" + ex.Message + ")");
                    }
                }
            }
        }
    }
}
