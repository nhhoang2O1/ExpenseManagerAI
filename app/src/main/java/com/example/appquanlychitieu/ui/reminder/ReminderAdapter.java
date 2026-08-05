package com.example.appquanlychitieu.ui.reminder;

import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.DiffUtil;
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

    public void setReminders(List<Reminder> reminders) {
        List<Reminder> next = reminders == null ? new ArrayList<>() : new ArrayList<>(reminders);
        List<Reminder> previous = this.reminders;
        DiffUtil.DiffResult result = DiffUtil.calculateDiff(new DiffUtil.Callback() {
            @Override public int getOldListSize() { return previous.size(); }
            @Override public int getNewListSize() { return next.size(); }
            @Override public boolean areItemsTheSame(int oldPosition, int newPosition) {
                Reminder oldItem = previous.get(oldPosition);
                Reminder newItem = next.get(newPosition);
                if (oldItem.getRemoteId() != null || newItem.getRemoteId() != null) {
                    return java.util.Objects.equals(oldItem.getRemoteId(), newItem.getRemoteId());
                }
                return oldItem.getId() == newItem.getId();
            }
            @Override public boolean areContentsTheSame(int oldPosition, int newPosition) {
                Reminder oldItem = previous.get(oldPosition);
                Reminder newItem = next.get(newPosition);
                return oldItem.getDayOfMonth() == newItem.getDayOfMonth()
                        && oldItem.getHour() == newItem.getHour()
                        && oldItem.getMinute() == newItem.getMinute()
                        && oldItem.getUserId() == newItem.getUserId()
                        && oldItem.isActive() == newItem.isActive()
                        && oldItem.getVersion() == newItem.getVersion()
                        && java.util.Objects.equals(oldItem.getContent(), newItem.getContent());
            }
        });
        this.reminders = next;
        result.dispatchUpdatesTo(this);
    }

    public void notifyReminderChanged(Reminder reminder) {
        int position = reminders.indexOf(reminder);
        if (position >= 0) notifyItemChanged(position);
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
