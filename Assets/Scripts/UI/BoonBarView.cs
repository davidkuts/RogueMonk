using Game.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// A row of pips for the boons held this run.
    ///
    /// Small, but not optional: a boon the player cannot see they own is the same failure as a buff
    /// they cannot feel. It also has to survive across levels, which is why it binds to
    /// <see cref="PlayerBoons"/> on the player rather than to anything a level owns.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoonBarView : MonoBehaviour
    {
        [SerializeField] PlayerBoons boons;
        [SerializeField, Tooltip("Pip prefab, cloned once per owned boon. Kept inactive as a template.")]
        RectTransform pipTemplate;
        [SerializeField] RectTransform container;
        [SerializeField] float pipSpacing = 34f;

        void Awake()
        {
            if (boons == null)
                boons = FindAnyObjectByType<PlayerBoons>();

            if (boons == null || pipTemplate == null || container == null)
            {
                enabled = false;
                return;
            }

            pipTemplate.gameObject.SetActive(false);
            boons.BoonsChanged += Rebuild;
            Rebuild();
        }

        void OnDestroy()
        {
            if (boons != null)
                boons.BoonsChanged -= Rebuild;
        }

        void Rebuild()
        {
            // Rebuilt wholesale rather than diffed: this happens a handful of times per run, and
            // the simplest correct version cannot drift out of sync with the loadout.
            for (int i = container.childCount - 1; i >= 0; i--)
            {
                Transform child = container.GetChild(i);
                if (child != pipTemplate)
                    Destroy(child.gameObject);
            }

            for (int i = 0; i < boons.Owned.Count; i++)
            {
                BoonDefinition boon = boons.Owned[i];
                RectTransform pip = Instantiate(pipTemplate, container);
                pip.gameObject.SetActive(true);
                pip.anchoredPosition = new Vector2(i * pipSpacing, 0f);

                var image = pip.GetComponent<Image>();
                if (image != null)
                    image.color = boon.Tint;

                var label = pip.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = boon.DisplayName.Length > 0 ? boon.DisplayName.Substring(0, 1) : "?";
                    label.color = Color.black;
                }
            }
        }
    }
}
