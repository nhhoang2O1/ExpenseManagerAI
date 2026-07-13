package com.example.appquanlychitieu.ui.reminder;

import android.annotation.SuppressLint;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;
import com.google.android.material.switchmaterial.SwitchMaterial;

import com.example.appquanlychitieu.R;
import com.example.appquanlychitieu.data.model.Reminder;

import java.util.ArrayList;
import java.util.List;

public class ReminderAdapter extends RecyclerView.Adapter<ReminderAdapter.ReminderViewHolder> {

    private List<Reminder> reminders = new ArrayList<>();
    private OnReminderClickListener listener;

    public interface OnReminderClickListener {
        void onReminderClick(Reminder reminder);
        void onReminderLongClick(Reminder reminder);
        void onReminderSwitchToggle(Reminder reminder, boolean isChecked);
        void onDeleteClick(Reminder reminder);
    }

    public void setOnReminderClickListener(OnReminderClickListener listener) {
        this.listener = listener;
    }

    @SuppressLint("NotifyDataSetChanged")
    public void setReminders(List<Reminder> reminders) {
        this.reminders = reminders;
        notifyDataSetChanged();
    }

    @NonNull
    @Override
    public ReminderViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        View view = LayoutInflater.from(parent.getContext()).inflate(R.layout.item_reminder, parent, false);
        return new ReminderViewHolder(view);
    }

    @Override
    public void onBindViewHolder(@NonNull ReminderViewHolder holder, int position) {
        Reminder reminder = reminders.get(position);
        holder.bind(reminder);
    }

    @Override
    public int getItemCount() {
        return reminders != null ? reminders.size() : 0;
    }

    class ReminderViewHolder extends RecyclerView.ViewHolder {
        TextView tvContent;
        TextView tvTime;
        SwitchMaterial switchActive;
        android.widget.ImageButton btnDelete;

        public ReminderViewHolder(@NonNull View itemView) {
            super(itemView);
            tvContent = itemView.findViewById(R.id.tv_content);
            tvTime = itemView.findViewById(R.id.tv_time);
            switchActive = itemView.findViewById(R.id.switch_active);
            btnDelete = itemView.findViewById(R.id.btn_delete);

            itemView.setOnClickListener(v -> {
                int position = getAdapterPosition();
                if (listener != null && position != RecyclerView.NO_POSITION) {
                    listener.onReminderClick(reminders.get(position));
                }
            });

            itemView.setOnLongClickListener(v -> {
                int position = getAdapterPosition();
                if (listener != null && position != RecyclerView.NO_POSITION) {
                    listener.onReminderLongClick(reminders.get(position));
                    return true;
                }
                return false;
            });

            switchActive.setOnCheckedChangeListener((buttonView, isChecked) -> {
                int position = getAdapterPosition();
                if (listener != null && position != RecyclerView.NO_POSITION && buttonView.isPressed()) {
                    listener.onReminderSwitchToggle(reminders.get(position), isChecked);
                }
            });
            
            btnDelete.setOnClickListener(v -> {
                int position = getAdapterPosition();
                if (listener != null && position != RecyclerView.NO_POSITION) {
                    listener.onDeleteClick(reminders.get(position));
                }
            });
        }

        @SuppressLint("SetTextI18n")
        public void bind(Reminder reminder) {
            tvContent.setText(reminder.getContent());
            String timeText = itemView.getContext().getString(
                    R.string.reminder_schedule,
                    reminder.getDayOfMonth(), reminder.getHour(), reminder.getMinute());
            tvTime.setText(timeText);
            switchActive.setChecked(reminder.isActive());
        }
    }
}
