/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Views/**/*.{cshtml,html}',
    './Pages/**/*.{cshtml,html}',
    './wwwroot/**/*.{js,jsx,ts,tsx}'
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['Roboto', 'sans-serif'],
      },
      colors: {
        primary: {
          50: '#fef9e7',
          100: '#fef3cf',
          200: '#fce89e',
          300: '#fadd6e',
          400: '#f7d13d',
          500: '#f4c430', // Jaune principal
          600: '#d4a700',
          700: '#a67f00',
          800: '#805f00',
          900: '#664d00',
        },
        dark: {
          50: '#f9fafb',
          100: '#f3f4f6',
          200: '#e5e7eb',
          300: '#d1d5db',
          400: '#9ca3af',
          500: '#6b7280',
          600: '#4b5563',
          700: '#374151', // Noir principal
          800: '#1f2937',
          900: '#111827',
        },
      },
      backgroundColor: {
        'primary-dark': '#111827',
        'primary-yellow': '#f4c430',
      },
      textColor: {
        'primary-dark': '#111827',
        'primary-yellow': '#f4c430',
      },
      borderColor: {
        'primary-dark': '#111827',
        'primary-yellow': '#f4c430',
      },
    },
  },
  plugins: [],
}
