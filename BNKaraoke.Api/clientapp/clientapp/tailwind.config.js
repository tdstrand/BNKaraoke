/** @type {import('tailwindcss').Config} */
module.exports = {
    content: ["./src/**/*.{ts,tsx}"],
    theme: {
        extend: {
            colors: {
                'blue-900': '#1E3A8A',
                'blue-600': '#3B82F6',
                'blue-100': '#BFDBFE',
                'coral-500': '#FF6B6B',
                'yellow-400': '#FFD93D',
            },
            fontFamily: {
                poppins: ['Poppins', 'sans-serif'],
            },
            animation: {
                bounce: 'bounce 1s infinite',
                pulse: 'pulse 2s infinite',
            },
        },
    },
    plugins: [],
};