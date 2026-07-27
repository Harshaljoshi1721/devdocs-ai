import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  // Produce a minimal, self-contained server build for Docker.
  output: "standalone",
};

export default nextConfig;
