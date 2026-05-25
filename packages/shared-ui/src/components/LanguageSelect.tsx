interface LanguageSelectOption {
  value: string;
  label: string;
}

interface LanguageSelectProps {
  label: string;
  value: string;
  options: LanguageSelectOption[];
  onChange: (value: string) => void;
}

export function LanguageSelect({ label, value, options, onChange }: LanguageSelectProps) {
  return (
    <label className="language-select">
      <span>{label}</span>
      <select value={value} onChange={(event) => onChange(event.target.value)}>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </label>
  );
}

export type { LanguageSelectOption, LanguageSelectProps };
