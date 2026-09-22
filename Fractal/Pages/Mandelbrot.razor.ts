export function getSize(): { width?: number, height?: number } {
  var e = document.getElementById("mandelbrot-tiles");
  if (!e) {
    return { width: undefined, height: undefined };
  }

  // console.log("getSize", e.clientWidth, e.clientHeight);

  return { width: e.clientWidth, height: e.clientHeight };
};
