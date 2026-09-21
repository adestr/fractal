// @ts-ignore
import { dotnet } from "/_framework/dotnet.js";

const { getAssemblyExports } = await dotnet.create();

interface WorkerMessage {
  realPointCount: number;
  imaginaryPointCount: number;
  realMin: number;
  realMax: number;
  imaginaryMin: number;
  imaginaryMax: number;
}

onmessage = async (e: any) => {
  console.log("Worker: Message received from main script", e);

  const exports = await getAssemblyExports("Fractal.dll");
  const real = e.data.realMin;
  const imaginary = e.data.imaginaryMin;
  console.log("Attempting to invoke calculate method", exports);

  try {
    const result = await exports.Fractal.Services.MandelbrotService.Calculate(
      e.data.realPointCount,
      e.data.imaginaryPointCount,
      e.data.realMin,
      e.data.realMax,
      e.data.imaginaryMin,
      e.data.imaginaryMax,
    );
    console.log("Returned from worker calculation");
    console.log(result);
    postMessage({
      status: "success",
      data: result,
    });
  } catch (error) {
    console.error("Error invoking calculate method", error);
  }
};

console.log("Worker: Starting up and waiting for messages...");
postMessage("ready");
