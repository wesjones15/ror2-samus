using System;
using System.Reflection;
using MonoMod.RuntimeDetour;
using RoR2;
using UnityEngine;
using UnityEngine.UI;

namespace Samus
{
    // Character select: the Melee fighters (plus Olimar, Mario and every DLC survivor) make the
    // survivor grid one row taller than its container reserves, so the details pane stacked below
    // covers the grid's bottom row. Give the grid's container one extra portrait row of height;
    // LeftHandPanel's VerticalLayoutGroup then places the whole details pane below it.
    //
    // The container height is set explicitly because RoR2's AdjustGridLayoutCellSize rewrites the
    // grid's cell size every frame, so the container's own layout group does not reliably report
    // the real number of rows.
    public static class SurvivorSelectLayout
    {
        static bool tried;
        static readonly BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static void Install()
        {
            if (tried) return;
            tried = true;
            var build = typeof(CharacterSelectBarController).GetMethod("Build", flags);
            if (build != null)
                new Hook(build,
                    (Action<Action<CharacterSelectBarController>, CharacterSelectBarController>)((orig, bar) => { orig(bar); Fit(bar); }));
        }

        static void Fit(CharacterSelectBarController bar)
        {
            var grid = bar.GetComponent<GridLayoutGroup>();
            if (!grid) return;
            var container = grid.transform.parent;
            if (!container) return;

            float height = ContainerHeight(grid);
            var layout = container.GetComponent<LayoutElement>() ?? container.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.minHeight = height;
            layout.flexibleHeight = 0f;
            LayoutRebuilder.MarkLayoutForRebuild(container as RectTransform);
            FitStripHighlights(container.parent);
            Debug.Log($"[Melee] survivor grid: {grid.transform.childCount} icons -> container '{container.name}' height {height}");
        }

        // The skill strips' HoverHighlight is a child of the strip's inset Inner, so the white hover
        // outline draws inside the row instead of over it. Expand it back out to the strip's edges.
        static void FitStripHighlights(Transform leftPanel)
        {
            if (!leftPanel) return;
            Transform skillPanel = null;
            foreach (var t in leftPanel.GetComponentsInChildren<Transform>(true))
                if (t.name == "SkillPanel") { skillPanel = t; break; }
            if (!skillPanel) return;
            foreach (var t in skillPanel.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "HoverHighlight") continue;
                var rt = (RectTransform)t;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(-4f, -4f);
                rt.offsetMax = new Vector2(4f, 4f);
            }
        }

        // The grid's real height (rows x portrait, plus padding) with one extra portrait row.
        static float ContainerHeight(GridLayoutGroup grid)
        {
            var cell = grid.cellSize;
            var spacing = grid.spacing;
            var pad = grid.padding;
            int columns = Mathf.Max(1, grid.constraintCount);
            int count = 0;
            for (int i = 0; i < grid.transform.childCount; i++)
                if (grid.transform.GetChild(i).gameObject.activeSelf) count++;
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)count / columns));
            float gridHeight = pad.vertical + rows * cell.y + (rows - 1) * spacing.y;
            return gridHeight + cell.y + spacing.y;
        }
    }
}
