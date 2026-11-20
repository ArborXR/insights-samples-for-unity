using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InsightsSamples.Common
{
    /// <summary>
    /// A shelf of cards that scrolls sideways inside the browser's page, which scrolls up and down. A scroll
    /// view keeps every wheel turn and drag that starts over it, so this one hands the vertical ones to the page.
    /// </summary>
    public class ShelfScroll : ScrollRect
    {
        private ScrollRect _page;
        private bool _draggingPage;

        private ScrollRect Page =>
            _page != null || transform.parent == null ? _page : _page = transform.parent.GetComponentInParent<ScrollRect>();

        public override void OnScroll(PointerEventData data)
        {
            if (Mathf.Abs(data.scrollDelta.y) > Mathf.Abs(data.scrollDelta.x))
            {
                if (Page != null) Page.OnScroll(data);
            }
            else base.OnScroll(data);
        }

        public override void OnInitializePotentialDrag(PointerEventData eventData)
        {
            if (Page != null) Page.OnInitializePotentialDrag(eventData);
            base.OnInitializePotentialDrag(eventData);
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            _draggingPage = Page != null && Mathf.Abs(eventData.delta.y) > Mathf.Abs(eventData.delta.x);
            if (_draggingPage) Page.OnBeginDrag(eventData);
            else base.OnBeginDrag(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (_draggingPage) Page.OnDrag(eventData);
            else base.OnDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (_draggingPage) Page.OnEndDrag(eventData);
            else base.OnEndDrag(eventData);
            _draggingPage = false;
        }
    }
}
