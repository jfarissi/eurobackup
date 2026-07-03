/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      colors: {
        surface: '#f7f9fb',
        primary: '#3525cd',
        'primary-container': '#4f46e5',
        'on-primary': '#ffffff',
        'on-primary-container': '#dad7ff',
        'on-surface': '#191c1e',
        'on-surface-variant': '#464555',
        'outline-variant': '#c7c4d8',
        outline: '#777587',
        'surface-container-lowest': '#ffffff',
        'surface-container-low': '#f2f4f6',
        'surface-container': '#eceef0',
        'surface-container-high': '#e6e8ea',
        secondary: '#505f76',
        'secondary-container': '#d0e1fb',
        tertiary: '#00524b',
        'tertiary-container': '#006c63',
        error: '#ba1a1a',
        'error-container': '#ffdad6',
      },
      fontFamily: {
        headline: ['Hanken Grotesk', 'system-ui', 'sans-serif'],
        body: ['Inter', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'ui-monospace', 'monospace'],
      },
      borderRadius: {
        DEFAULT: '0.125rem',
        lg: '0.25rem',
        xl: '0.5rem',
      },
      maxWidth: {
        'container-max': '1440px',
      },
    },
  },
  plugins: [],
};
