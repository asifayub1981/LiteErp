import { definePreset } from '@primeng/themes';
import Aura from '@primeng/themes/aura';

// LiteERP "Soft Professional" preset — indigo primary, soft surfaces.
export const LiteErpPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#eef2ff',
      100: '#e0e7ff',
      200: '#c7d2fe',
      300: '#a5b4fc',
      400: '#818cf8',
      500: '#6366f1',
      600: '#4f46e5',
      700: '#4338ca',
      800: '#3730a3',
      900: '#312e81',
      950: '#1e1b4b',
    },
  },
  components: {
    card: {
      colorScheme: {
        light: {
          root: {
            background: '#ffffff',
            borderRadius: '14px',
            shadow: '0 1px 3px rgba(30,27,75,.06), 0 4px 14px rgba(30,27,75,.05)',
          },
        },
      },
    },
    button: {
      root: {
        borderRadius: '10px',
      },
    },
    inputtext: {
      root: {
        borderRadius: '10px',
      },
    },
  },
});
