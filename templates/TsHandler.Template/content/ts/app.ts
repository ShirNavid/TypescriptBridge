// Sample TypeScript entry point.
// Edit this file and rebuild. The build produces a C# readonly field
// TypescriptProvider.TypescriptCode containing the compiled JavaScript.

export function greet(name: string): string {
    return `Hello, ${name}!`;
}

export function add(a: number, b: number): number {
    return a + b;
}

console.log(greet("TsHandler"));
console.log("2 + 3 =", add(2, 3));
