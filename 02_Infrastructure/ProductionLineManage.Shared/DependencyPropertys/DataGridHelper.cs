using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ProductionLineManage.Shared.DependencyPropertys
{
    /// <summary>DataGrid 附加属性辅助类</summary>
    public static class DataGridHelper
    {
        #region ===================== 行展开/折叠 =====================

        /// <summary>行展开状态附加属性</summary>
        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.RegisterAttached(
                "IsExpanded",
                typeof(bool),
                typeof(DataGridHelper),
                new PropertyMetadata(false, OnIsExpandedChanged));

        /// <summary>设置行展开状态</summary>
        public static void SetIsExpanded(DependencyObject obj, bool value)
        {
            obj.SetValue(IsExpandedProperty, value);
        }

        /// <summary>获取行展开状态</summary>
        public static bool GetIsExpanded(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsExpandedProperty);
        }

        /// <summary>展开状态变化回调，同步 DetailsVisibility</summary>
        private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var row = d as DataGridRow;
            if (row != null)
            {
                row.DetailsVisibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        #endregion
    }
}
