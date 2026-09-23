// @ts-ignore
import { dotnet } from "/_framework/dotnet.js";

let assemblyExports: any;
let startupError: any;

try {
  const { getAssemblyExports, getConfig } = await dotnet.create();
  const config = getConfig();
  assemblyExports = await getAssemblyExports(config.mainAssemblyName);
} catch (err) {
  startupError = err;
}

interface FractalMessage {
  type: "mandelbrot";
  requestId: string;
}

interface MandelbrotMessage extends FractalMessage {
  payload: {
    requestId: number;
    nr: number;
    ni: number;
    rMin: number;
    rMax: number;
    iMin: number;
    iMax: number;
    maxIterations: number;
  };
}

self.addEventListener(
  "message",
  async (event: MessageEvent<MandelbrotMessage>) => {
    try {
      if (!assemblyExports) {
        throw new Error(startupError || "Worker exports could not be loaded");
      }

      let result: any;
      switch (event.data.type) {
        case "mandelbrot":
          const p = event.data.payload;
          const t = new Date().getTime();
          console.log(`[${p.requestId}] Sending Mandelbrot calculation request to C# layer`);
          result =
            assemblyExports.Fractal.Workers.MandelbrotCalculation.Calculate(
              p.requestId,
              p.nr,
              p.ni,
              p.rMin,
              p.rMax,
              p.iMin,
              p.iMax,
              p.maxIterations,
            );
          const diff = new Date().getTime() - t;
          console.log(`[${p.requestId}] Received Mandelbrot calculation response from C# layer in ${diff} ms`);
          break;

        default:
          throw new Error(`Unknown message type: ${event.data.type}`);
      }

      const x = new Int32Array(result.length);
      result.copyTo(x);

      (self as unknown as Worker).postMessage({
        status: "success",
        requestId: event.data.requestId,
        result: x
      });
    } catch (err) {
      self.postMessage({
        requestId: event.data.requestId,
        error: (err as Error)?.message || "Unknown error",
      });
      console.error(err);
    }
  },
);

self.postMessage({
  status: "ready",
});
