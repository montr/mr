import { KeyboardEvent, PointerEvent, useEffect, useRef, useState } from "react";

export function Splitter() {
  const [mobile, setMobile] = useState(() => window.matchMedia("(max-width: 850px)").matches);
  const [value, setValue] = useState(mobile ? 50 : 45);
  const dragging = useRef(false);

  useEffect(() => {
    const media = window.matchMedia("(max-width: 850px)");
    const update = () => {
      const nextMobile = media.matches;
      const nextValue = nextMobile ? 50 : 45;
      setMobile(nextMobile);
      setValue(nextValue);
      document.documentElement.style.setProperty(nextMobile ? "--source-height" : "--left-width", `${nextValue}%`);
    };
    update();
    media.addEventListener("change", update);
    return () => {
      media.removeEventListener("change", update);
      document.documentElement.style.removeProperty("--source-height");
      document.documentElement.style.removeProperty("--left-width");
    };
  }, []);

  function apply(next: number) {
    const clamped = Math.max(mobile ? 20 : 25, Math.min(mobile ? 80 : 75, next));
    setValue(clamped);
    document.documentElement.style.setProperty(mobile ? "--source-height" : "--left-width", `${clamped}%`);
  }

  function pointerMove(event: PointerEvent<HTMLDivElement>) {
    if (!dragging.current) return;
    const layout = event.currentTarget.parentElement;
    if (!layout) return;
    const bounds = layout.getBoundingClientRect();
    apply(mobile ? ((event.clientY - bounds.top) / bounds.height) * 100 : ((event.clientX - bounds.left) / bounds.width) * 100);
  }

  function keyDown(event: KeyboardEvent<HTMLDivElement>) {
    const delta = mobile ? (event.key === "ArrowDown" ? 3 : event.key === "ArrowUp" ? -3 : 0) : (event.key === "ArrowRight" ? 3 : event.key === "ArrowLeft" ? -3 : 0);
    if (!delta) return;
    event.preventDefault();
    apply(value + delta);
  }

  return <div
    className={`splitter ${mobile ? "horizontal" : "vertical"}`}
    role="separator"
    aria-label="Resize input and preview"
    aria-orientation={mobile ? "horizontal" : "vertical"}
    aria-valuemin={mobile ? 20 : 25}
    aria-valuemax={mobile ? 80 : 75}
    aria-valuenow={Math.round(value)}
    tabIndex={0}
    onKeyDown={keyDown}
    onPointerDown={event => { dragging.current = true; event.currentTarget.setPointerCapture(event.pointerId); }}
    onPointerMove={pointerMove}
    onPointerUp={() => { dragging.current = false; }}
    onPointerCancel={() => { dragging.current = false; }}
  />;
}
